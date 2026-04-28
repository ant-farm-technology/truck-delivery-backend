import math
import time

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


class VrpSolver:
    def solve(self, request: OptimizeRequest) -> OptimizeResponse:
        start = time.perf_counter()
        data = _build_vrp_data(request)

        result = self._run_ortools(data)
        strategy = "vrp"

        if not result.feasible or not result.routes:
            logger.warning("VRP infeasible, falling back to greedy", orders=len(request.orders))
            result = _greedy_solve(data)
            strategy = "greedy"

        elapsed_ms = (time.perf_counter() - start) * 1000
        return _build_response(request, data, result, elapsed_ms, strategy)

    def _run_ortools(self, data: VrpData) -> VrpResult:
        num_locations = len(data.distance_matrix)
        num_vehicles = len(data.vehicles)

        manager = pywrapcp.RoutingIndexManager(num_locations, num_vehicles, data.depot_index)
        routing = pywrapcp.RoutingModel(manager)

        # Distance callback
        def distance_callback(from_idx: int, to_idx: int) -> int:
            from_node = manager.IndexToNode(from_idx)
            to_node = manager.IndexToNode(to_idx)
            return data.distance_matrix[from_node][to_node]

        transit_cb_idx = routing.RegisterTransitCallback(distance_callback)
        routing.SetArcCostEvaluatorOfAllVehicles(transit_cb_idx)

        # Weight capacity constraint — precompute to avoid O(n) inside callback.
        precomputed_demands = _build_demands(data, num_locations)

        def weight_demand_callback(idx: int) -> int:
            return precomputed_demands[manager.IndexToNode(idx)]

        weight_cb_idx = routing.RegisterUnaryTransitCallback(weight_demand_callback)
        routing.AddDimensionWithVehicleCapacity(
            weight_cb_idx,
            0,  # no slack
            [int(v.max_weight_kg * 10) for v in data.vehicles],  # scale to int
            True,  # start cumul at zero
            "Weight",
        )

        # Time window constraint (optional)
        if data.time_matrix:
            def time_callback(from_idx: int, to_idx: int) -> int:
                from_node = manager.IndexToNode(from_idx)
                to_node = manager.IndexToNode(to_idx)
                return data.time_matrix[from_node][to_node]  # type: ignore[index]

            time_cb_idx = routing.RegisterTransitCallback(time_callback)
            routing.AddDimension(time_cb_idx, 3600, 86400, False, "Time")

        # Penalty for dropping orders (prefer assigning all)
        penalty = int(settings.weight_penalty * max(
            data.distance_matrix[i][j]
            for i in range(num_locations)
            for j in range(num_locations)
            if i != j
        ) if num_locations > 1 else 100_000)

        for node in data.nodes:
            routing.AddDisjunction([manager.NodeToIndex(node.location_index)], penalty)

        # Search params
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
            return VrpResult(routes=[], unassigned_node_indices=list(range(len(data.nodes))), total_distance_m=0, feasible=False)

        return _extract_solution(manager, routing, solution, data)


def _build_vrp_data(request: OptimizeRequest) -> VrpData:
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
    for order in request.orders:
        pickup_idx = len(nodes)
        delivery_idx = pickup_idx + 1
        nodes.append(VrpNode(
            location_index=order.pickup_index,
            order_id=order.order_id,
            is_pickup=True,
            weight_kg=order.weight_kg,
            volume_cbm=order.volume_cbm,
            paired_node_index=delivery_idx,
        ))
        nodes.append(VrpNode(
            location_index=order.delivery_index,
            order_id=order.order_id,
            is_pickup=False,
            weight_kg=-order.weight_kg,
            volume_cbm=-order.volume_cbm,
            paired_node_index=pickup_idx,
        ))

    timeout = request.solver_timeout_seconds or settings.solver_timeout_seconds

    return VrpData(
        distance_matrix=request.distance_matrix,
        time_matrix=request.time_matrix,
        vehicles=vehicles,
        nodes=nodes,
        depot_index=request.depot_index,
        solver_timeout_seconds=timeout,
        weight_capacity=[v.max_weight_kg for v in vehicles],
        volume_capacity=[v.max_volume_cbm for v in vehicles],
    )


def _build_demands(data: VrpData, num_locations: int) -> list[int]:
    demands = [0] * num_locations
    for node in data.nodes:
        # Scale by 10 to keep integer precision for kg with 1 decimal
        demands[node.location_index] += int(node.weight_kg * 10)
    return demands


def _extract_solution(
    manager: pywrapcp.RoutingIndexManager,
    routing: pywrapcp.RoutingModel,
    solution: pywrapcp.Assignment,
    data: VrpData,
) -> VrpResult:
    routes: list[VrpRoute] = []
    total_distance = 0

    node_location_map = {n.location_index: n for n in data.nodes}

    for vehicle_idx in range(len(data.vehicles)):
        if routing.IsVehicleUsed(solution, vehicle_idx):
            index = routing.Start(vehicle_idx)
            route_locations: list[int] = []
            route_nodes: list[int] = []
            route_distance = 0
            route_weight = 0.0

            while not routing.IsEnd(index):
                loc = manager.IndexToNode(index)
                route_locations.append(loc)
                if loc in node_location_map:
                    node = node_location_map[loc]
                    node_idx = next(i for i, n in enumerate(data.nodes) if n.location_index == loc)
                    route_nodes.append(node_idx)
                    if node.is_pickup:
                        route_weight += node.weight_kg
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

    assigned_node_indices = {ni for r in routes for ni in r.node_indices}
    unassigned = [i for i in range(len(data.nodes)) if i not in assigned_node_indices and data.nodes[i].is_pickup]

    return VrpResult(routes=routes, unassigned_node_indices=unassigned, total_distance_m=total_distance, feasible=True)


def _greedy_solve(data: VrpData) -> VrpResult:
    """Nearest-driver greedy assignment as fallback."""
    routes: list[VrpRoute] = []
    assigned_order_ids: set = set()
    total_distance = 0

    for vehicle in data.vehicles:
        route_nodes: list[int] = []
        route_locations: list[int] = [vehicle.start_index]
        route_distance = 0
        route_weight = 0.0
        current_location = vehicle.start_index

        for i, node in enumerate(data.nodes):
            if not node.is_pickup:
                continue
            if node.order_id in assigned_order_ids:
                continue
            if route_weight + node.weight_kg > vehicle.max_weight_kg:
                continue

            delivery_node = data.nodes[node.paired_node_index]
            leg = (
                data.distance_matrix[current_location][node.location_index]
                + data.distance_matrix[node.location_index][delivery_node.location_index]
            )

            route_nodes.extend([i, node.paired_node_index])
            route_locations.extend([node.location_index, delivery_node.location_index])
            route_distance += leg
            route_weight += node.weight_kg
            current_location = delivery_node.location_index
            assigned_order_ids.add(node.order_id)

        if route_nodes:
            routes.append(VrpRoute(
                vehicle_index=vehicle.index,
                node_indices=route_nodes,
                location_indices=route_locations,
                total_distance_m=route_distance,
                total_weight_kg=route_weight,
            ))
            total_distance += route_distance

    unassigned = [
        i for i, n in enumerate(data.nodes)
        if n.is_pickup and n.order_id not in assigned_order_ids
    ]

    return VrpResult(routes=routes, unassigned_node_indices=unassigned, total_distance_m=total_distance, feasible=True)


def _build_response(
    request: OptimizeRequest,
    data: VrpData,
    result: VrpResult,
    elapsed_ms: float,
    strategy: str,
) -> OptimizeResponse:
    assignments: list[DriverAssignment] = []
    for route in result.routes:
        vehicle = data.vehicles[route.vehicle_index]
        driver_info = request.drivers[route.vehicle_index]

        order_ids = list({
            data.nodes[ni].order_id
            for ni in route.node_indices
        })

        assignments.append(DriverAssignment(
            driver_id=vehicle.driver_id,
            vehicle_id=vehicle.vehicle_id,
            order_ids=order_ids,
            route_indices=route.location_indices,
            total_distance_m=route.total_distance_m,
            total_weight_kg=route.total_weight_kg,
        ))

    unassigned_ids = list({
        data.nodes[ni].order_id
        for ni in result.unassigned_node_indices
    })

    return OptimizeResponse(
        assignments=assignments,
        unassigned_order_ids=unassigned_ids,
        total_distance_m=result.total_distance_m,
        solve_time_ms=elapsed_ms,
        strategy_used=strategy,
        feasible=result.feasible,
    )
