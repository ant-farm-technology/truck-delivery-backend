use axum::extract::State;
use axum::Json;
use serde::{Deserialize, Serialize};
use std::collections::hash_map::DefaultHasher;
use std::hash::{Hash, Hasher};
use tokio::task::JoinSet;

use crate::cache::Cache;
use crate::domain::haversine;
use crate::error::AppError;
use crate::state::AppState;

const MATRIX_CACHE_TTL: u64 = 900; // 15 min
const MAX_LOCATIONS: usize = 20;
const AVG_TRUCK_SPEED_KMH: f64 = 40.0;

#[derive(Deserialize)]
pub struct MatrixRequest {
    pub locations: Vec<Location>,
}

#[derive(Deserialize, Serialize, Clone)]
pub struct Location {
    pub lat: f64,
    pub lng: f64,
}

#[derive(Serialize, Deserialize)]
pub struct MatrixResponse {
    /// distances[i][j] = distance in km from location i to location j.
    pub distances_km: Vec<Vec<f64>>,
    /// durations[i][j] = estimated travel time in minutes.
    pub durations_minutes: Vec<Vec<f64>>,
}

pub async fn build_matrix(
    State(state): State<AppState>,
    Json(req): Json<MatrixRequest>,
) -> Result<Json<MatrixResponse>, AppError> {
    let n = req.locations.len();

    if n < 2 {
        return Err(AppError::BadRequest("at least 2 locations required".into()));
    }
    if n > MAX_LOCATIONS {
        return Err(AppError::BadRequest(
            format!("maximum {MAX_LOCATIONS} locations allowed"),
        ));
    }

    let cache_key = format!("matrix:{}", hash_locations(&req.locations));
    let mut cache = Cache::new(state.redis.clone());

    if let Some(cached) = cache.get::<MatrixResponse>(&cache_key).await {
        return Ok(Json(cached));
    }

    // Compute all N×N pairs in parallel.
    let locations = req.locations;
    let mut set: JoinSet<(usize, usize, f64)> = JoinSet::new();

    for i in 0..n {
        for j in 0..n {
            let from = locations[i].clone();
            let to = locations[j].clone();
            set.spawn(async move {
                let dist = if i == j {
                    0.0
                } else {
                    haversine::distance_km(from.lat, from.lng, to.lat, to.lng)
                };
                (i, j, dist)
            });
        }
    }

    let mut distances_km = vec![vec![0.0f64; n]; n];
    let mut durations_minutes = vec![vec![0.0f64; n]; n];

    while let Some(Ok((i, j, dist))) = set.join_next().await {
        let duration = (dist / AVG_TRUCK_SPEED_KMH) * 60.0;
        distances_km[i][j] = dist;
        durations_minutes[i][j] = duration;
    }

    let response = MatrixResponse { distances_km, durations_minutes };
    cache.set(&cache_key, &response, MATRIX_CACHE_TTL).await;

    Ok(Json(response))
}

fn hash_locations(locations: &[Location]) -> u64 {
    let mut hasher = DefaultHasher::new();
    for loc in locations {
        // Truncate to 4 decimal places (~11 m precision) before hashing.
        let lat = (loc.lat * 10_000.0).round() as i64;
        let lng = (loc.lng * 10_000.0).round() as i64;
        lat.hash(&mut hasher);
        lng.hash(&mut hasher);
    }
    hasher.finish()
}
