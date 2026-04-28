from dataclasses import dataclass, field
from uuid import UUID


@dataclass
class VrpVehicle:
    index: int
    driver_id: UUID
    vehicle_id: UUID
    start_index: int
    max_weight_kg: float
    max_volume_cbm: float


@dataclass
class VrpNode:
    """Represents a pickup or delivery stop in the VRP graph."""
    location_index: int
    order_id: UUID
    is_pickup: bool
    weight_kg: float
    volume_cbm: float
    # paired_node_index: index of the corresponding delivery (for pickup) or pickup (for delivery)
    paired_node_index: int = -1


@dataclass
class VrpData:
    distance_matrix: list[list[int]]
    time_matrix: list[list[int]] | None
    vehicles: list[VrpVehicle]
    nodes: list[VrpNode]  # pickup-delivery pairs in order: [p0, d0, p1, d1, ...]
    depot_index: int
    solver_timeout_seconds: int
    weight_capacity: list[float] = field(default_factory=list)
    volume_capacity: list[float] = field(default_factory=list)


@dataclass
class VrpRoute:
    vehicle_index: int
    node_indices: list[int]  # indices into VrpData.nodes
    location_indices: list[int]  # indices into distance_matrix
    total_distance_m: int
    total_weight_kg: float


@dataclass
class VrpResult:
    routes: list[VrpRoute]
    unassigned_node_indices: list[int]
    total_distance_m: int
    feasible: bool
