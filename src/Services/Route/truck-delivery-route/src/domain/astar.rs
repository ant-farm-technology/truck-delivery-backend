use std::cmp::Ordering;
use std::collections::{BinaryHeap, HashMap};

use super::haversine;

#[derive(Debug, Clone)]
pub struct GraphNode {
    pub id: i64,
    pub lat: f64,
    pub lng: f64,
}

#[derive(Debug, Clone)]
pub struct GraphEdge {
    pub to_node: i64,
    pub weight_m: f64,
}

pub struct Graph {
    pub nodes: HashMap<i64, GraphNode>,
    pub adjacency: HashMap<i64, Vec<GraphEdge>>,
}

impl Graph {
    pub fn new(nodes: Vec<GraphNode>, edges: Vec<(i64, GraphEdge)>) -> Self {
        let node_map: HashMap<i64, GraphNode> = nodes.into_iter().map(|n| (n.id, n)).collect();
        let mut adjacency: HashMap<i64, Vec<GraphEdge>> = HashMap::new();
        for (from, edge) in edges {
            adjacency.entry(from).or_default().push(edge);
        }
        Self { nodes: node_map, adjacency }
    }

    pub fn is_empty(&self) -> bool {
        self.nodes.is_empty()
    }
}

#[derive(Debug, Clone)]
pub struct RouteResult {
    /// Ordered waypoints [[lat, lng], ...].
    pub path: Vec<[f64; 2]>,
    pub distance_m: f64,
    pub duration_s: f64,
}

#[derive(Debug)]
struct HeapEntry {
    f_score: f64,
    g_score: f64,
    node_id: i64,
}

impl PartialEq for HeapEntry {
    fn eq(&self, other: &Self) -> bool {
        self.f_score == other.f_score
    }
}
impl Eq for HeapEntry {}

impl PartialOrd for HeapEntry {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

// BinaryHeap is a max-heap; we want min-f_score, so reverse the comparison.
impl Ord for HeapEntry {
    fn cmp(&self, other: &Self) -> Ordering {
        other.f_score.partial_cmp(&self.f_score).unwrap_or(Ordering::Equal)
    }
}

/// A* search from `start_id` to `goal_id` over `graph`.
/// Returns `None` if no path exists.
pub fn find_path(graph: &Graph, start_id: i64, goal_id: i64) -> Option<RouteResult> {
    let goal_node = graph.nodes.get(&goal_id)?;

    let mut g_score: HashMap<i64, f64> = HashMap::new();
    let mut came_from: HashMap<i64, i64> = HashMap::new();
    let mut heap = BinaryHeap::new();

    let start_node = graph.nodes.get(&start_id)?;
    let h0 = heuristic(start_node, goal_node);

    g_score.insert(start_id, 0.0);
    heap.push(HeapEntry { f_score: h0, g_score: 0.0, node_id: start_id });

    while let Some(HeapEntry { g_score: g, node_id, .. }) = heap.pop() {
        if node_id == goal_id {
            return Some(build_result(graph, &came_from, goal_id, g));
        }

        // Skip stale heap entries.
        if g > *g_score.get(&node_id).unwrap_or(&f64::INFINITY) {
            continue;
        }

        let edges = match graph.adjacency.get(&node_id) {
            Some(e) => e,
            None => continue,
        };

        for edge in edges {
            let next_g = g + edge.weight_m;
            if next_g < *g_score.get(&edge.to_node).unwrap_or(&f64::INFINITY) {
                g_score.insert(edge.to_node, next_g);
                came_from.insert(edge.to_node, node_id);

                if let Some(next_node) = graph.nodes.get(&edge.to_node) {
                    let h = heuristic(next_node, goal_node);
                    heap.push(HeapEntry { f_score: next_g + h, g_score: next_g, node_id: edge.to_node });
                }
            }
        }
    }

    None
}

fn heuristic(from: &GraphNode, to: &GraphNode) -> f64 {
    haversine::distance_km(from.lat, from.lng, to.lat, to.lng) * 1000.0
}

fn build_result(
    graph: &Graph,
    came_from: &HashMap<i64, i64>,
    goal_id: i64,
    distance_m: f64,
) -> RouteResult {
    let mut path = Vec::new();
    let mut current = goal_id;
    loop {
        if let Some(node) = graph.nodes.get(&current) {
            path.push([node.lat, node.lng]);
        }
        match came_from.get(&current) {
            Some(&prev) => current = prev,
            None => break,
        }
    }
    path.reverse();

    // Assume average truck speed 40 km/h for duration estimate.
    let duration_s = distance_m / (40.0 * 1000.0 / 3600.0);

    RouteResult { path, distance_m, duration_s }
}
