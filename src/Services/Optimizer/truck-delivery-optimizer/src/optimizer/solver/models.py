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
    """Represents a pickup or delivery stop.
    Pairs are stored as: nodes[2*i]=pickup[i], nodes[2*i+1]=delivery[i].
    OR-Tools graph node index = list index + 1 (node 0 is reserved for depot).
    """
    location_index: int   # index into distance_matrix / time_matrix
    order_id: UUID
    is_pickup: bool
    weight_kg: float      # positive for pickup, negative for delivery
    volume_cbm: float
    paired_node_index: int = -1  # index into nodes list
    # Time window in seconds from route reference time
    tw_start_s: int = 0
    tw_end_s: int = 172800  # 48-hour horizon default
    sla_tier: str | None = None


@dataclass
class VrpData:
    distance_matrix: list[list[int]]
    time_matrix: list[list[int]] | None
    vehicles: list[VrpVehicle]
    nodes: list[VrpNode]  # pickup-delivery pairs: [p0, d0, p1, d1, ...]
    # node_locations[i] = distance_matrix index for OR-Tools graph node i.
    # Index 0 = depot, 2*j+1 = pickup[j], 2*j+2 = delivery[j].
    node_locations: list[int]
    depot_index: int
    solver_timeout_seconds: int
    weight_capacity: list[float] = field(default_factory=list)
    volume_capacity: list[float] = field(default_factory=list)
    lifo_enabled: bool = False
    # cluster_indices[k] = vehicle cluster (0..num_vehicles-1) for order pair at nodes[2k]
    order_cluster_indices: list[int] = field(default_factory=list)


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
