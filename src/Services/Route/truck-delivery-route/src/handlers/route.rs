use axum::extract::{Query, State};
use axum::Json;
use serde::{Deserialize, Serialize};

use crate::cache::Cache;
use crate::domain::{astar, haversine};
use crate::error::AppError;
use crate::state::AppState;

const ROUTE_CACHE_TTL: u64 = 1800; // 30 min
const AVG_TRUCK_SPEED_KMH: f64 = 40.0;

#[derive(Deserialize)]
pub struct RouteQuery {
    pub from_lat: f64,
    pub from_lng: f64,
    pub to_lat: f64,
    pub to_lng: f64,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RouteResponse {
    pub distance_km: f64,
    pub estimated_duration_minutes: f64,
    /// Ordered waypoints [[lat, lng], ...]. Empty when using Haversine fallback.
    pub path: Vec<[f64; 2]>,
    pub source: RouteSource,
}

#[derive(Serialize, Deserialize, Clone, PartialEq)]
#[serde(rename_all = "lowercase")]
pub enum RouteSource {
    Astar,
    Haversine,
    Cache,
}

pub async fn calculate_route(
    State(state): State<AppState>,
    Query(params): Query<RouteQuery>,
) -> Result<Json<RouteResponse>, AppError> {
    if params.from_lat == params.to_lat && params.from_lng == params.to_lng {
        return Ok(Json(RouteResponse {
            distance_km: 0.0,
            estimated_duration_minutes: 0.0,
            path: vec![[params.from_lat, params.from_lng]],
            source: RouteSource::Haversine,
        }));
    }

    let cache_key = format!(
        "route:{:.4}:{:.4}:{:.4}:{:.4}",
        params.from_lat, params.from_lng, params.to_lat, params.to_lng
    );

    let mut cache = Cache::new(state.redis.clone());

    if let Some(mut cached) = cache.get::<RouteResponse>(&cache_key).await {
        cached.source = RouteSource::Cache;
        return Ok(Json(cached));
    }

    let response = match try_astar_route(&state, &params).await {
        Some(r) => r,
        None => haversine_route(&params),
    };

    cache.set(&cache_key, &response, ROUTE_CACHE_TTL).await;

    Ok(Json(response))
}

/// Attempt A* pathfinding through the PostGIS road network.
/// Returns None if road data is unavailable or no path exists.
async fn try_astar_route(state: &AppState, params: &RouteQuery) -> Option<RouteResponse> {
    // Find the nearest road node to each endpoint.
    let from_node = nearest_node(&state.db, params.from_lat, params.from_lng).await?;
    let to_node = nearest_node(&state.db, params.to_lat, params.to_lng).await?;

    if from_node == to_node {
        return None;
    }

    // Load graph within an expanded bounding box around the route.
    let graph = load_subgraph(&state.db, params).await?;
    if graph.is_empty() {
        return None;
    }

    let result = astar::find_path(&graph, from_node, to_node)?;
    let distance_km = result.distance_m / 1000.0;
    let duration_min = result.duration_s / 60.0;

    Some(RouteResponse {
        distance_km,
        estimated_duration_minutes: duration_min,
        path: result.path,
        source: RouteSource::Astar,
    })
}

/// Straight-line Haversine fallback when road network is unavailable.
fn haversine_route(params: &RouteQuery) -> RouteResponse {
    let distance_km = haversine::distance_km(
        params.from_lat, params.from_lng,
        params.to_lat, params.to_lng,
    );
    let duration_min = (distance_km / AVG_TRUCK_SPEED_KMH) * 60.0;

    tracing::warn!(
        from_lat = params.from_lat, from_lng = params.from_lng,
        to_lat = params.to_lat, to_lng = params.to_lng,
        "A* unavailable, falling back to Haversine"
    );

    RouteResponse {
        distance_km,
        estimated_duration_minutes: duration_min,
        path: vec![
            [params.from_lat, params.from_lng],
            [params.to_lat, params.to_lng],
        ],
        source: RouteSource::Haversine,
    }
}

async fn nearest_node(pool: &sqlx::PgPool, lat: f64, lng: f64) -> Option<i64> {
    let row: Option<(i64,)> = sqlx::query_as(
        r#"
        SELECT id FROM nodes
        ORDER BY geom <-> ST_SetSRID(ST_MakePoint($1, $2), 4326)
        LIMIT 1
        "#,
    )
    .bind(lng)
    .bind(lat)
    .fetch_optional(pool)
    .await
    .ok()
    .flatten();

    row.map(|(id,)| id)
}

async fn load_subgraph(pool: &sqlx::PgPool, params: &RouteQuery) -> Option<astar::Graph> {
    // Expand bounding box by 20 % on each side to capture detours.
    let lat_min = params.from_lat.min(params.to_lat);
    let lat_max = params.from_lat.max(params.to_lat);
    let lng_min = params.from_lng.min(params.to_lng);
    let lng_max = params.from_lng.max(params.to_lng);
    let lat_pad = (lat_max - lat_min).max(0.01) * 0.2;
    let lng_pad = (lng_max - lng_min).max(0.01) * 0.2;

    let bbox_min_lat = lat_min - lat_pad;
    let bbox_max_lat = lat_max + lat_pad;
    let bbox_min_lng = lng_min - lng_pad;
    let bbox_max_lng = lng_max + lng_pad;

    #[derive(sqlx::FromRow)]
    struct NodeRow { id: i64, lat: f64, lng: f64 }

    let node_rows: Vec<NodeRow> = sqlx::query_as(
        r#"
        SELECT id, ST_Y(geom) AS lat, ST_X(geom) AS lng
        FROM nodes
        WHERE geom && ST_MakeEnvelope($1, $2, $3, $4, 4326)
        "#,
    )
    .bind(bbox_min_lng).bind(bbox_min_lat)
    .bind(bbox_max_lng).bind(bbox_max_lat)
    .fetch_all(pool)
    .await
    .ok()?;

    if node_rows.is_empty() {
        return None;
    }

    let node_ids: Vec<i64> = node_rows.iter().map(|r| r.id).collect();

    #[derive(sqlx::FromRow)]
    struct EdgeRow { from_node: i64, to_node: i64, weight_m: f64 }

    let edge_rows: Vec<EdgeRow> = sqlx::query_as(
        r#"
        SELECT from_node, to_node, weight_m
        FROM edges
        WHERE from_node = ANY($1)
        "#,
    )
    .bind(&node_ids)
    .fetch_all(pool)
    .await
    .ok()?;

    let nodes: Vec<astar::GraphNode> = node_rows
        .into_iter()
        .map(|r| astar::GraphNode { id: r.id, lat: r.lat, lng: r.lng })
        .collect();

    let edges: Vec<(i64, astar::GraphEdge)> = edge_rows
        .into_iter()
        .map(|r| (r.from_node, astar::GraphEdge { to_node: r.to_node, weight_m: r.weight_m }))
        .collect();

    Some(astar::Graph::new(nodes, edges))
}
