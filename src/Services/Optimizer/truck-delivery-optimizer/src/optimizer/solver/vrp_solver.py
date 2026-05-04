import time as _time

import structlog
from ortools.constraint_solver import pywrapcp, routing_enums_pb2

from optimizer.config import settings
from optimizer.models.request import OptimizeRequest
from optimizer.models.response import DriverAssignment, OptimizeResponse
from optimizer.solver.models import VrpData, VrpNode, VrpResult, VrpRoute, VrpVehicle

logger = structlog.get_logger(__name__)

_STRATEGY_MAP = {
    "PATH_CHEAPEST_ARC": routing_enums_pb2.FirstSolutionStrategy.PATH_CHEAPEST_ARC,
    "SAVINGS": routing_enums_pb2.FirstSolutionStrategy.SAVINGS,
    "CHRISTOFIDES": routing_enums_pb2.FirstSolutionStrategy.CHRISTOFIDES,
}

_METAHEURISTIC_MAP = {
    "GUIDED_LOCAL_SEARCH": routing_enums_pb2.LocalSearchMetaheuristic.GUIDED_LOCAL_SEARCH,
    "SIMULATED_ANNEALING": routing_enums_pb2.LocalSearchMetaheuristic.SIMULATED_ANNEALING,
    "TABU_SEARCH": routing_enums_pb2.LocalSearchMetaheuristic.TABU_SEARCH,
}

# Drop penalty multiplier by SLA tier — express orders are much more expensive to miss.
_SLA_PENALTY_MULTIPLIER: dict[str, float] = {
    "express": 3.0,
    "standard": 1.0,
    "economy": 0.5,
}

_MAX_TW_S = 172800  # 48-hour time horizon
# LIFO pairwise constraints grow O(n²); cap to avoid excessive solve time.
_LIFO_MAX_ORDERS = 30


class VrpSolver:
    def solve(self, request: OptimizeRequest) -> OptimizeResponse:
        start = _time.perf_counter()
        data = _build_vrp_data(request)

        result = self._run_ortools(data)
        strategy = "vrp-lifo" if data.lifo_enabled else "vrp"

        if not result.feasible or not result.routes:
            logger.warning("VRP infeasible, falling back to greedy", orders=len(request.orders))
            result = _greedy_solve(data)
            strategy = "greedy-lifo" if data.lifo_enabled else "greedy"

        elapsed_ms = (_time.perf_counter() - start) * 1000
        return _build_response(data, result, elapsed_ms, strategy)

    def _run_ortools(self, data: VrpData) -> VrpResult:
        num_orders = len(data.nodes) // 2
        num_vehicles = len(data.vehicles)

        if num_orders == 0:
            return VrpResult(routes=[], unassigned_node_indices=[], total_distance_m=0, feasible=True)

        # OR-Tools graph: node 0 = depot, 2i+1 = pickup[i], 2i+2 = delivery[i]
        num_nodes = 2 * num_orders + 1
        manager = pywrapcp.RoutingIndexManager(num_nodes, num_vehicles, 0)
        routing = pywrapcp.RoutingModel(manager)

        # Distance callback — maps OR-Tools node → distance matrix location
        def distance_callback(from_idx: int, to_idx: int) -> int:
            from_loc = data.node_locations[manager.IndexToNode(from_idx)]
            to_loc = data.node_locations[manager.IndexToNode(to_idx)]
            return data.distance_matrix[from_loc][to_loc]

        transit_cb = routing.RegisterTransitCallback(distance_callback)
        routing.SetArcCostEvaluatorOfAllVehicles(transit_cb)

        # Weight capacity (×10 scale for decimal kg precision)
        def weight_demand(idx: int) -> int:
            node = manager.IndexToNode(idx)
            if node == 0:
                return 0
            return int(data.nodes[node - 1].weight_kg * 10)

        weight_cb = routing.RegisterUnaryTransitCallback(weight_demand)
        routing.AddDimensionWithVehicleCapacity(
            weight_cb,
            0,
            [int(v.max_weight_kg * 10) for v in data.vehicles],
            True,
            "Weight",
        )

        # Optional time dimension — only active when caller provides time_matrix
        time_dim = None
        if data.time_matrix is not None:
            def time_callback(from_idx: int, to_idx: int) -> int:
                from_loc = data.node_locations[manager.IndexToNode(from_idx)]
                to_loc = data.node_locations[manager.IndexToNode(to_idx)]
                return data.time_matrix[from_loc][to_loc]  # type: ignore[index]

            time_cb = routing.RegisterTransitCallback(time_callback)
            # slack=3600 allows waiting up to 1h at a node; horizon = 48h
            routing.AddDimension(time_cb, 3600, _MAX_TW_S, False, "Time")
            time_dim = routing.GetDimensionOrDie("Time")

        # Base drop penalty = weight_penalty × longest edge in graph
        max_dist = (
            max(
                data.distance_matrix[i][j]
                for i in range(len(data.distance_matrix))
                for j in range(len(data.distance_matrix))
                if i != j
            )
            if len(data.distance_matrix) > 1
            else 100_000
        )
        base_penalty = int(settings.weight_penalty * max_dist)

        # Per-order VRPPD constraints + time windows + SLA-scaled penalties
        for i in range(num_orders):
            pickup_rt = manager.NodeToIndex(2 * i + 1)
            delivery_rt = manager.NodeToIndex(2 * i + 2)
            vrp_pickup = data.nodes[2 * i]
            vrp_delivery = data.nodes[2 * i + 1]

            # VRPPD: same vehicle must serve pickup then delivery
            routing.AddPickupAndDelivery(pickup_rt, delivery_rt)
            routing.solver().Add(
                routing.VehicleVar(pickup_rt) == routing.VehicleVar(delivery_rt)
            )

            # Time window constraints (requires time_matrix)
            if time_dim is not None:
                if vrp_pickup.tw_start_s > 0:
                    time_dim.CumulVar(pickup_rt).SetMin(vrp_pickup.tw_start_s)
                if vrp_pickup.tw_end_s < _MAX_TW_S:
                    time_dim.CumulVar(pickup_rt).SetMax(vrp_pickup.tw_end_s)
                if vrp_delivery.tw_end_s < _MAX_TW_S:
                    time_dim.CumulVar(delivery_rt).SetMax(vrp_delivery.tw_end_s)
                # Enforce pickup-before-delivery in time
                routing.solver().Add(
                    time_dim.CumulVar(pickup_rt) <= time_dim.CumulVar(delivery_rt)
                )

            # SLA-weighted drop penalty — separate disjunctions, each penalised once
            tier_mult = _SLA_PENALTY_MULTIPLIER.get(vrp_pickup.sla_tier or "standard", 1.0)
            penalty = int(base_penalty * tier_mult)
            routing.AddDisjunction([pickup_rt], penalty)
            routing.AddDisjunction([delivery_rt], penalty)

        # LIFO pairwise constraints — requires time dimension for CumulVar ordering
        if data.lifo_enabled:
            if time_dim is not None:
                _add_lifo_constraints(routing, manager, time_dim, num_orders)
            else:
                logger.warning(
                    "LIFO requested but time_matrix not provided; "
                    "OR-Tools LIFO constraints skipped — greedy fallback will be LIFO-aware"
                )

        search_params = pywrapcp.DefaultRoutingSearchParameters()
        search_params.first_solution_strategy = _STRATEGY_MAP.get(
            settings.solver_first_solution_strategy,
            routing_enums_pb2.FirstSolutionStrategy.PATH_CHEAPEST_ARC,
        )
        search_params.local_search_metaheuristic = _METAHEURISTIC_MAP.get(
            settings.solver_local_search_metaheuristic,
            routing_enums_pb2.LocalSearchMetaheuristic.GUIDED_LOCAL_SEARCH,
        )
        search_params.time_limit.seconds = data.solver_timeout_seconds

        solution = routing.SolveWithParameters(search_params)
        if not solution:
            return VrpResult(
                routes=[],
                unassigned_node_indices=list(range(0, len(data.nodes), 2)),
                total_distance_m=0,
                feasible=False,
            )

        return _extract_solution(manager, routing, solution, data)


def _cluster_orders(
    delivery_locs: list[int],
    num_clusters: int,
    distance_matrix: list[list[int]],
) -> list[int]:
    """K-medoids style clustering via farthest-first initialisation.

    Uses only the distance matrix — no coordinates required.
    Returns cluster index (0..min(num_clusters,n)-1) for each order.
    """
    n = len(delivery_locs)
    if n == 0:
        return []
    k = min(num_clusters, n)
    if k <= 1:
        return [0] * n

    # Iteratively pick the order whose delivery loc is farthest from all current medoids
    medoid_indices: list[int] = [0]
    while len(medoid_indices) < k:
        best_dist, best_i = -1, -1
        for i in range(n):
            if i in medoid_indices:
                continue
            d = min(
                distance_matrix[delivery_locs[i]][delivery_locs[m]]
                for m in medoid_indices
            )
            if d > best_dist:
                best_dist, best_i = d, i
        if best_i == -1:
            break
        medoid_indices.append(best_i)

    medoid_locs = [delivery_locs[m] for m in medoid_indices]
    return [
        min(range(len(medoid_locs)), key=lambda c: distance_matrix[loc][medoid_locs[c]])
        for loc in delivery_locs
    ]


def _add_lifo_constraints(
    routing: pywrapcp.RoutingModel,
    manager: pywrapcp.RoutingIndexManager,
    time_dim,
    num_orders: int,
) -> None:
    """Add O(n²) pairwise LIFO constraints via CumulVar ordering.

    For each pair (i, j) on the same vehicle:
      pickup_i before pickup_j  ↔  delivery_j before delivery_i

    Encoded as: lifo_ok >= same_vehicle
    where lifo_ok = IsEqual(b_pi_le_pj, b_dj_le_di).
    """
    if num_orders > _LIFO_MAX_ORDERS:
        logger.warning(
            "LIFO enabled with large order count; skipping OR-Tools LIFO constraints to avoid timeout",
            num_orders=num_orders,
            limit=_LIFO_MAX_ORDERS,
        )
        return

    solver = routing.solver()
    for i in range(num_orders):
        pi = manager.NodeToIndex(2 * i + 1)
        di = manager.NodeToIndex(2 * i + 2)
        for j in range(i + 1, num_orders):
            pj = manager.NodeToIndex(2 * j + 1)
            dj = manager.NodeToIndex(2 * j + 2)

            same_veh = solver.IsEqualVar(routing.VehicleVar(pi), routing.VehicleVar(pj))
            b_pi_le_pj = solver.IsLessOrEqualVar(time_dim.CumulVar(pi), time_dim.CumulVar(pj))
            b_dj_le_di = solver.IsLessOrEqualVar(time_dim.CumulVar(dj), time_dim.CumulVar(di))
            # LIFO: pickup order reversed from delivery order ↔ b_pi_le_pj == b_dj_le_di
            lifo_ok = solver.IsEqualVar(b_pi_le_pj, b_dj_le_di)
            # Implication: same_veh → lifo_ok  (lifo_ok >= same_veh)
            solver.Add(lifo_ok >= same_veh)


def _build_vrp_data(request: OptimizeRequest) -> VrpData:
    now_s = int(_time.time())

    vehicles = [
        VrpVehicle(
            index=i,
            driver_id=d.driver_id,
            vehicle_id=d.vehicle_id,
            start_index=d.location_index,
            max_weight_kg=d.max_weight_kg,
            max_volume_cbm=d.max_volume_cbm,
        )
        for i, d in enumerate(request.drivers)
    ]

    nodes: list[VrpNode] = []
    # node_locations[0] = depot; thereafter: pickup0_loc, delivery0_loc, pickup1_loc, ...
    node_locations: list[int] = [request.depot_index]

    for order in request.orders:
        # Resolve time windows: Unix timestamps take priority over legacy relative seconds
        pickup_tw_start = 0
        pickup_tw_end = _MAX_TW_S
        delivery_tw_end = _MAX_TW_S

        if order.earliest_pickup_unix is not None:
            pickup_tw_start = max(0, order.earliest_pickup_unix - now_s)
        elif order.earliest_pickup is not None:
            pickup_tw_start = order.earliest_pickup

        if order.hard_deadline_unix is not None:
            delivery_tw_end = max(0, order.hard_deadline_unix - now_s)
        elif order.latest_delivery is not None:
            delivery_tw_end = order.latest_delivery

        pickup_node_idx = len(nodes)
        delivery_node_idx = pickup_node_idx + 1

        nodes.append(VrpNode(
            location_index=order.pickup_index,
            order_id=order.order_id,
            is_pickup=True,
            weight_kg=order.weight_kg,
            volume_cbm=order.volume_cbm,
            paired_node_index=delivery_node_idx,
            tw_start_s=pickup_tw_start,
            tw_end_s=pickup_tw_end,
            sla_tier=order.sla_tier,
        ))
        nodes.append(VrpNode(
            location_index=order.delivery_index,
            order_id=order.order_id,
            is_pickup=False,
            weight_kg=-order.weight_kg,
            volume_cbm=-order.volume_cbm,
            paired_node_index=pickup_node_idx,
            tw_start_s=0,
            tw_end_s=delivery_tw_end,
            sla_tier=order.sla_tier,
        ))

        # OR-Tools node 2*i+1 = pickup, 2*i+2 = delivery
        node_locations.append(order.pickup_index)
        node_locations.append(order.delivery_index)

    timeout = request.solver_timeout_seconds or settings.solver_timeout_seconds

    # K-medoids clustering by delivery location — helps greedy assign geographically
    # close orders to the same vehicle and improves OR-Tools warm-start quality.
    delivery_locs = [order.delivery_index for order in request.orders]
    cluster_indices = _cluster_orders(delivery_locs, len(vehicles), request.distance_matrix)

    return VrpData(
        distance_matrix=request.distance_matrix,
        time_matrix=request.time_matrix,
        vehicles=vehicles,
        nodes=nodes,
        node_locations=node_locations,
        depot_index=request.depot_index,
        solver_timeout_seconds=timeout,
        weight_capacity=[v.max_weight_kg for v in vehicles],
        volume_capacity=[v.max_volume_cbm for v in vehicles],
        lifo_enabled=request.enable_lifo,
        order_cluster_indices=cluster_indices,
    )


def _extract_solution(
    manager: pywrapcp.RoutingIndexManager,
    routing: pywrapcp.RoutingModel,
    solution: pywrapcp.Assignment,
    data: VrpData,
) -> VrpResult:
    routes: list[VrpRoute] = []
    total_distance = 0

    for vehicle_idx in range(len(data.vehicles)):
        if not routing.IsVehicleUsed(solution, vehicle_idx):
            continue

        index = routing.Start(vehicle_idx)
        route_nodes: list[int] = []     # indices into data.nodes
        route_locations: list[int] = [] # distance_matrix indices
        route_distance = 0
        route_weight = 0.0

        while not routing.IsEnd(index):
            node = manager.IndexToNode(index)
            route_locations.append(data.node_locations[node])
            if node > 0:  # skip depot
                vrp_node_idx = node - 1
                route_nodes.append(vrp_node_idx)
                if data.nodes[vrp_node_idx].is_pickup:
                    route_weight += data.nodes[vrp_node_idx].weight_kg
            next_index = solution.Value(routing.NextVar(index))
            route_distance += routing.GetArcCostForVehicle(index, next_index, vehicle_idx)
            index = next_index

        routes.append(VrpRoute(
            vehicle_index=vehicle_idx,
            node_indices=route_nodes,
            location_indices=route_locations,
            total_distance_m=route_distance,
            total_weight_kg=route_weight,
        ))
        total_distance += route_distance

    assigned = {ni for r in routes for ni in r.node_indices}
    # Report unassigned by pickup node index (even indices)
    unassigned = [i for i in range(0, len(data.nodes), 2) if i not in assigned]

    return VrpResult(
        routes=routes,
        unassigned_node_indices=unassigned,
        total_distance_m=total_distance,
        feasible=True,
    )


def _greedy_solve(data: VrpData) -> VrpResult:
    """Cluster-aware greedy assignment with optional LIFO route construction.

    Standard mode: pickup→delivery pairs interleaved, cluster-priority ordering.
    LIFO mode:     all pickups (farthest-delivery-first) then all deliveries (nearest-first).
                   This ensures the last package loaded is the first delivered — LIFO compliant.
    """
    routes: list[VrpRoute] = []
    assigned_order_ids: set = set()
    total_distance = 0
    num_orders = len(data.nodes) // 2
    depot_loc = data.node_locations[0]
    clusters = data.order_cluster_indices

    for v_idx, vehicle in enumerate(data.vehicles):
        # Cluster-priority: orders in this vehicle's cluster are offered first
        order_priority = sorted(
            range(num_orders),
            key=lambda k: (0 if clusters and clusters[k] == v_idx else 1),
        )

        selected: list[int] = []  # order k → pickup at nodes[2k], delivery at nodes[2k+1]
        route_weight = 0.0

        for k in order_priority:
            pickup = data.nodes[2 * k]
            if pickup.order_id in assigned_order_ids:
                continue
            if route_weight + pickup.weight_kg > vehicle.max_weight_kg:
                continue
            selected.append(k)
            route_weight += pickup.weight_kg
            assigned_order_ids.add(pickup.order_id)

        if not selected:
            continue

        if data.lifo_enabled:
            # Sort orders so nearest delivery comes first in the delivery sequence.
            # Pickup order = reverse of delivery order → LIFO stack behaviour.
            # Route shape: depot → P(last) → … → P(first) → D(first) → … → D(last)
            selected.sort(
                key=lambda k: data.distance_matrix[depot_loc][data.nodes[2 * k + 1].location_index]
            )
            route_nodes: list[int] = []
            route_locations: list[int] = [depot_loc]
            for k in reversed(selected):  # farthest-delivery loaded first (bottom of stack)
                route_nodes.append(2 * k)
                route_locations.append(data.nodes[2 * k].location_index)
            for k in selected:  # nearest delivery unloaded first (top of stack)
                route_nodes.append(2 * k + 1)
                route_locations.append(data.nodes[2 * k + 1].location_index)
        else:
            route_nodes = []
            route_locations = [depot_loc]
            for k in selected:
                route_nodes.extend([2 * k, 2 * k + 1])
                route_locations.extend([
                    data.nodes[2 * k].location_index,
                    data.nodes[2 * k + 1].location_index,
                ])

        route_distance = sum(
            data.distance_matrix[route_locations[i]][route_locations[i + 1]]
            for i in range(len(route_locations) - 1)
        )

        routes.append(VrpRoute(
            vehicle_index=vehicle.index,
            node_indices=route_nodes,
            location_indices=route_locations,
            total_distance_m=route_distance,
            total_weight_kg=route_weight,
        ))
        total_distance += route_distance

    unassigned = [
        2 * k for k in range(num_orders)
        if data.nodes[2 * k].order_id not in assigned_order_ids
    ]

    return VrpResult(
        routes=routes,
        unassigned_node_indices=unassigned,
        total_distance_m=total_distance,
        feasible=True,
    )


def _build_response(
    data: VrpData,
    result: VrpResult,
    elapsed_ms: float,
    strategy: str,
) -> OptimizeResponse:
    assignments: list[DriverAssignment] = []
    for route in result.routes:
        vehicle = data.vehicles[route.vehicle_index]
        order_ids = list({data.nodes[ni].order_id for ni in route.node_indices})

        assignments.append(DriverAssignment(
            driver_id=vehicle.driver_id,
            vehicle_id=vehicle.vehicle_id,
            order_ids=order_ids,
            route_indices=route.location_indices,
            total_distance_m=route.total_distance_m,
            total_weight_kg=route.total_weight_kg,
        ))

    unassigned_ids = list({data.nodes[ni].order_id for ni in result.unassigned_node_indices})

    return OptimizeResponse(
        assignments=assignments,
        unassigned_order_ids=unassigned_ids,
        total_distance_m=result.total_distance_m,
        solve_time_ms=elapsed_ms,
        strategy_used=strategy,
        feasible=result.feasible,
    )
