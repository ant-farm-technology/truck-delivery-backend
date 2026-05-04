from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

from optimizer.main import create_app
from optimizer.models.request import DriverInfo, OptimizeRequest, OrderInfo
from optimizer.solver.vrp_solver import VrpSolver

# 4-location distance matrix: depot(0), pickup_A(1), delivery_A(2), pickup_B(3)
DISTANCE_MATRIX = [
    [0,   100, 200, 150],
    [100,   0, 120, 80],
    [200, 120,   0, 90],
    [150,  80,  90,  0],
]

DRIVER_1 = DriverInfo(
    driver_id=uuid4(),
    vehicle_id=uuid4(),
    location_index=0,
    max_weight_kg=1000.0,
    max_volume_cbm=10.0,
)

ORDER_A = OrderInfo(
    order_id=uuid4(),
    pickup_index=1,
    delivery_index=2,
    weight_kg=200.0,
    volume_cbm=2.0,
)

ORDER_B = OrderInfo(
    order_id=uuid4(),
    pickup_index=3,
    delivery_index=2,
    weight_kg=300.0,
    volume_cbm=3.0,
)


class TestVrpSolver:
    def test_single_driver_single_order(self) -> None:
        solver = VrpSolver()
        request = OptimizeRequest(
            drivers=[DRIVER_1],
            orders=[ORDER_A],
            distance_matrix=DISTANCE_MATRIX,
            depot_index=0,
            solver_timeout_seconds=5,
        )
        response = solver.solve(request)

        assert response.feasible
        assert len(response.assignments) == 1
        assert ORDER_A.order_id in response.assignments[0].order_ids
        assert len(response.unassigned_order_ids) == 0
        assert response.solve_time_ms > 0

    def test_single_driver_two_orders_within_capacity(self) -> None:
        solver = VrpSolver()
        request = OptimizeRequest(
            drivers=[DRIVER_1],
            orders=[ORDER_A, ORDER_B],
            distance_matrix=DISTANCE_MATRIX,
            depot_index=0,
            solver_timeout_seconds=5,
        )
        response = solver.solve(request)

        assert response.feasible
        total_assigned = sum(len(a.order_ids) for a in response.assignments)
        assert total_assigned + len(response.unassigned_order_ids) == 2

    def test_order_exceeds_capacity_goes_unassigned(self) -> None:
        heavy_driver = DriverInfo(
            driver_id=uuid4(),
            vehicle_id=uuid4(),
            location_index=0,
            max_weight_kg=100.0,  # too small for ORDER_A (200kg)
            max_volume_cbm=10.0,
        )
        solver = VrpSolver()
        request = OptimizeRequest(
            drivers=[heavy_driver],
            orders=[ORDER_A],
            distance_matrix=DISTANCE_MATRIX,
            depot_index=0,
            solver_timeout_seconds=5,
        )
        response = solver.solve(request)

        # Either strategy should leave this unassigned
        assert len(response.unassigned_order_ids) == 1

    def test_strategy_reported(self) -> None:
        solver = VrpSolver()
        request = OptimizeRequest(
            drivers=[DRIVER_1],
            orders=[ORDER_A],
            distance_matrix=DISTANCE_MATRIX,
            depot_index=0,
            solver_timeout_seconds=5,
        )
        response = solver.solve(request)

        assert response.strategy_used in ("vrp", "greedy")


class TestHealthRoutes:
    def test_liveness(self) -> None:
        client = TestClient(create_app())
        resp = client.get("/health")
        assert resp.status_code == 200
        assert resp.json()["status"] == "healthy"

    def test_readiness(self) -> None:
        client = TestClient(create_app())
        resp = client.get("/ready")
        assert resp.status_code == 200


class TestOptimizeRoute:
    def test_optimize_endpoint(self) -> None:
        client = TestClient(create_app())
        payload = {
            "drivers": [
                {
                    "driver_id": str(uuid4()),
                    "vehicle_id": str(uuid4()),
                    "location_index": 0,
                    "max_weight_kg": 1000.0,
                    "max_volume_cbm": 10.0,
                }
            ],
            "orders": [
                {
                    "order_id": str(uuid4()),
                    "pickup_index": 1,
                    "delivery_index": 2,
                    "weight_kg": 200.0,
                    "volume_cbm": 2.0,
                }
            ],
            "distance_matrix": DISTANCE_MATRIX,
            "depot_index": 0,
            "solver_timeout_seconds": 5,
        }
        resp = client.post("/optimize", json=payload)
        assert resp.status_code == 200
        data = resp.json()
        assert "assignments" in data
        assert "solve_time_ms" in data
        assert "strategy_used" in data

    def test_optimize_invalid_payload_returns_422(self) -> None:
        client = TestClient(create_app())
        resp = client.post("/optimize", json={"drivers": [], "orders": []})
        assert resp.status_code == 422
