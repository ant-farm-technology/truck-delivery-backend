from uuid import UUID

from pydantic import BaseModel, Field


class TruckDimensions(BaseModel):
    length_m: float = Field(gt=0)
    width_m: float = Field(gt=0)
    height_m: float = Field(gt=0)
    max_weight_kg: float = Field(gt=0)


class PackageInput(BaseModel):
    package_id: UUID
    length_m: float = Field(gt=0)
    width_m: float = Field(gt=0)
    height_m: float = Field(gt=0)
    weight_kg: float = Field(gt=0)
    delivery_rank: int = Field(ge=1, description="1=delivered first (nearest door)")
    can_tilt: bool = False
    value: float = Field(default=0.0, ge=0, description="Monetary value for priority scoring")
    received_at_unix: int | None = Field(default=None, description="Unix timestamp when order was received")
    hard_deadline_unix: int | None = Field(default=None, description="Hard delivery deadline unix timestamp")


class BinCheckRequest(BaseModel):
    truck: TruckDimensions
    packages: list[PackageInput] = Field(min_length=1)
    current_time_unix: int | None = Field(default=None, description="Current time unix. Defaults to server time.")


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
    # SLA fields (Unix timestamps — preferred over legacy relative fields)
    earliest_pickup_unix: int | None = Field(default=None, description="Earliest pickup Unix timestamp")
    hard_deadline_unix: int | None = Field(default=None, description="Hard delivery deadline Unix timestamp")
    desired_delivery_unix: int | None = Field(default=None, description="Desired (soft) delivery Unix timestamp")
    sla_tier: str | None = Field(default=None, description="SLA tier: express | standard | economy")
    # Legacy relative fields (seconds from now) — deprecated, prefer Unix timestamps
    earliest_pickup: int | None = Field(default=None, ge=0, description="Seconds from now (deprecated)")
    latest_delivery: int | None = Field(default=None, ge=0, description="Seconds from now (deprecated)")


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
    enable_lifo: bool = Field(default=False, description="Enforce LIFO (Last-In-First-Out) loading order — requires time_matrix for OR-Tools constraints; greedy fallback is always LIFO-aware when enabled")
