from uuid import UUID

from pydantic import BaseModel, Field


class DriverInfo(BaseModel):
    driver_id: UUID
    vehicle_id: UUID
    location_index: int = Field(ge=0, description="Index in distance_matrix for current location")
    max_weight_kg: float = Field(gt=0)
    max_volume_cbm: float = Field(gt=0)


class OrderInfo(BaseModel):
    order_id: UUID
    pickup_index: int = Field(ge=0, description="Index in distance_matrix for pickup point")
    delivery_index: int = Field(ge=0, description="Index in distance_matrix for delivery point")
    weight_kg: float = Field(gt=0)
    volume_cbm: float = Field(gt=0)
    earliest_pickup: int | None = Field(default=None, ge=0, description="Seconds from now")
    latest_delivery: int | None = Field(default=None, ge=0, description="Seconds from now")


class OptimizeRequest(BaseModel):
    """Input for VRP optimization. Distance matrix provided by Route service."""

    correlation_id: str | None = None
    drivers: list[DriverInfo] = Field(min_length=1)
    orders: list[OrderInfo] = Field(min_length=1)
    # distance_matrix[i][j] = distance in meters between location i and j
    distance_matrix: list[list[int]] = Field(min_length=2)
    # time_matrix[i][j] = travel time in seconds (optional, used for time windows)
    time_matrix: list[list[int]] | None = None
    depot_index: int = Field(default=0, ge=0, description="Depot location index")
    solver_timeout_seconds: int | None = Field(default=None, ge=1, le=30)
