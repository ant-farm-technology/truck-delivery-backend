from uuid import UUID

from pydantic import BaseModel


class RejectedPackage(BaseModel):
    package_id: UUID
    reason: str  # "dimension_too_large" | "exceeds_weight" | "exceeds_volume" | "lower_priority"
    priority_score: float


class BinCheckResponse(BaseModel):
    all_fit: bool
    accepted_package_ids: list[UUID]
    rejected_packages: list[RejectedPackage]
    loading_sequence: list[UUID]
    requires_tilt: bool
    requires_dispatcher_confirmation: bool
    accessibility_ok: bool
    accessibility_warnings: list[str]
    total_weight_kg: float
    weight_utilization_pct: float
    volume_utilization_pct: float


class DriverAssignment(BaseModel):
    driver_id: UUID
    vehicle_id: UUID
    order_ids: list[UUID]
    route_indices: list[int]
    total_distance_m: int
    total_weight_kg: float


class OptimizeResponse(BaseModel):
    assignments: list[DriverAssignment]
    unassigned_order_ids: list[UUID]
    total_distance_m: int
    solve_time_ms: float
    strategy_used: str  # "vrp" | "greedy"
    feasible: bool
