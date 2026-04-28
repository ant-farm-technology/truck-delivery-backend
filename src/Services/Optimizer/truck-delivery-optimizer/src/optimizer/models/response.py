from uuid import UUID

from pydantic import BaseModel


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
