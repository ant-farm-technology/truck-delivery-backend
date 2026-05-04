import uuid

import pytest
from fastapi.testclient import TestClient

from optimizer.main import create_app
from optimizer.models.request import BinCheckRequest, PackageInput, TruckDimensions
from optimizer.solver.bin_packing_solver import BinPackingSolver

TRUCK = TruckDimensions(length_m=6.0, width_m=2.4, height_m=2.2, max_weight_kg=3000.0)


def _pkg(
    rank: int,
    l: float,
    w: float,
    h: float,
    weight: float = 100.0,
    value: float = 1000.0,
    can_tilt: bool = False,
) -> PackageInput:
    return PackageInput(
        package_id=uuid.uuid4(),
        length_m=l,
        width_m=w,
        height_m=h,
        weight_kg=weight,
        delivery_rank=rank,
        can_tilt=can_tilt,
        value=value,
    )


@pytest.fixture
def solver() -> BinPackingSolver:
    return BinPackingSolver()


class TestFeasibility:
    def test_single_package_fits(self, solver: BinPackingSolver) -> None:
        req = BinCheckRequest(truck=TRUCK, packages=[_pkg(1, 2.0, 1.0, 1.0)])
        result = solver.check(req)
        assert result.all_fit is True
        assert len(result.accepted_package_ids) == 1
        assert result.rejected_packages == []

    def test_package_too_large_in_all_orientations(self, solver: BinPackingSolver) -> None:
        # Bigger than truck in every axis-aligned orientation
        req = BinCheckRequest(truck=TRUCK, packages=[_pkg(1, 7.0, 3.0, 3.0)])
        result = solver.check(req)
        assert result.all_fit is False
        assert len(result.accepted_package_ids) == 0
        assert result.rejected_packages[0].reason == "dimension_too_large"

    def test_two_packages_both_fit(self, solver: BinPackingSolver) -> None:
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[
                _pkg(1, 2.0, 1.0, 1.0, weight=500.0),
                _pkg(2, 2.0, 1.0, 1.0, weight=500.0),
            ],
        )
        result = solver.check(req)
        assert result.all_fit is True
        assert len(result.accepted_package_ids) == 2

    def test_weight_overflow_triggers_priority_selection(self, solver: BinPackingSolver) -> None:
        # Two packages, combined weight exceeds limit
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[
                _pkg(1, 2.0, 1.0, 1.0, weight=2000.0, value=5000.0),
                _pkg(2, 2.0, 1.0, 1.0, weight=2000.0, value=100.0),
            ],
        )
        result = solver.check(req)
        assert result.all_fit is False
        assert len(result.accepted_package_ids) == 1
        assert len(result.rejected_packages) == 1
        assert result.rejected_packages[0].reason == "exceeds_weight"
        # Higher value package (rank=1) should be accepted
        assert result.accepted_package_ids[0] == req.packages[0].package_id

    def test_volume_overflow_triggers_priority_selection(self, solver: BinPackingSolver) -> None:
        # Fill truck volume exactly with one large package, second should be rejected
        truck_vol = TRUCK.length_m * TRUCK.width_m * TRUCK.height_m
        big_l = TRUCK.length_m * 0.95
        big_w = TRUCK.width_m * 0.95
        big_h = TRUCK.height_m * 0.95
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[
                _pkg(1, big_l, big_w, big_h, weight=100.0, value=9000.0),
                _pkg(2, 1.0, 1.0, 1.0, weight=50.0, value=100.0),
            ],
        )
        result = solver.check(req)
        assert result.all_fit is False
        assert len(result.accepted_package_ids) == 1
        assert result.rejected_packages[0].reason == "exceeds_volume"


class TestDiagonalPlacement:
    def test_package_fits_only_with_tilt(self, solver: BinPackingSolver) -> None:
        import math
        # Package length > truck length but <= diagonal of truck floor
        diag = math.sqrt(TRUCK.length_m**2 + TRUCK.width_m**2)
        long_l = TRUCK.length_m + 0.5
        assert long_l <= diag, "Test setup: package must fit diagonally but not straight"
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[_pkg(1, long_l, 0.5, 0.5, can_tilt=True)],
        )
        result = solver.check(req)
        assert result.all_fit is True
        assert result.requires_tilt is True
        assert result.requires_dispatcher_confirmation is True

    def test_package_cannot_tilt_if_flag_false(self, solver: BinPackingSolver) -> None:
        long_l = TRUCK.length_m + 0.5
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[_pkg(1, long_l, 0.5, 0.5, can_tilt=False)],
        )
        result = solver.check(req)
        assert result.all_fit is False
        assert result.rejected_packages[0].reason == "dimension_too_large"


class TestLoadingSequence:
    def test_loading_sequence_is_reverse_of_delivery(self, solver: BinPackingSolver) -> None:
        pkg1 = _pkg(1, 1.0, 1.0, 1.0)  # delivered first → loaded last (near door)
        pkg2 = _pkg(2, 1.0, 1.0, 1.0)  # delivered second → loaded first (deepest)
        pkg3 = _pkg(3, 1.0, 1.0, 1.0)  # delivered third → loaded first-est (deepest of all)
        req = BinCheckRequest(truck=TRUCK, packages=[pkg1, pkg2, pkg3])
        result = solver.check(req)
        seq = result.loading_sequence
        # Sequence should be [pkg3, pkg2, pkg1] — highest rank first (deepest)
        id_to_rank = {p.package_id: p.delivery_rank for p in req.packages}
        ranks_in_seq = [id_to_rank[pid] for pid in seq]
        assert ranks_in_seq == sorted(ranks_in_seq, reverse=True)

    def test_accessibility_ok_for_normal_sequence(self, solver: BinPackingSolver) -> None:
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[
                _pkg(1, 2.0, 1.0, 1.0),
                _pkg(2, 2.0, 1.0, 1.0),
            ],
        )
        result = solver.check(req)
        assert result.accessibility_ok is True
        assert result.accessibility_warnings == []

    def test_accessibility_warns_on_duplicate_ranks(self, solver: BinPackingSolver) -> None:
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[
                _pkg(1, 1.0, 1.0, 1.0),
                _pkg(1, 1.0, 1.0, 1.0),  # same rank
            ],
        )
        result = solver.check(req)
        assert result.accessibility_ok is False
        assert any("Duplicate" in w for w in result.accessibility_warnings)


class TestUtilization:
    def test_utilization_within_bounds(self, solver: BinPackingSolver) -> None:
        req = BinCheckRequest(
            truck=TRUCK,
            packages=[_pkg(1, 2.0, 1.0, 1.0, weight=500.0)],
        )
        result = solver.check(req)
        assert 0 < result.weight_utilization_pct <= 100
        assert 0 < result.volume_utilization_pct <= 100
        assert result.total_weight_kg == 500.0


@pytest.fixture
def client() -> TestClient:
    return TestClient(create_app())


class TestBinCheckEndpoint:
    def test_endpoint_returns_200(self, client: TestClient) -> None:
        payload = {
            "truck": {"length_m": 6.0, "width_m": 2.4, "height_m": 2.2, "max_weight_kg": 3000.0},
            "packages": [
                {
                    "package_id": str(uuid.uuid4()),
                    "length_m": 2.0,
                    "width_m": 1.0,
                    "height_m": 1.0,
                    "weight_kg": 100.0,
                    "delivery_rank": 1,
                    "can_tilt": False,
                    "value": 500.0,
                }
            ],
        }
        resp = client.post("/bin-check", json=payload)
        assert resp.status_code == 200
        data = resp.json()
        assert "all_fit" in data
        assert "accepted_package_ids" in data
        assert "loading_sequence" in data

    def test_endpoint_returns_422_on_invalid_payload(self, client: TestClient) -> None:
        resp = client.post("/bin-check", json={"truck": {}, "packages": []})
        assert resp.status_code == 422
