use axum::{
    extract::{Path, Query, State},
    http::StatusCode,
    Json,
};
use chrono::Utc;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::error::AppError;
use crate::state::AppState;

// ---------------------------------------------------------------------------
// Nearby drivers
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
pub struct NearbyQuery {
    pub lat: f64,
    pub lng: f64,
    pub radius_km: f64,
    /// Minimum vehicle capacity required by the order.
    pub min_weight_kg: Option<f64>,
}

#[derive(Serialize, sqlx::FromRow)]
pub struct NearbyDriver {
    pub driver_id: Uuid,
    pub distance_km: f64,
    pub max_weight_kg: f64,
    pub status: String,
}

pub async fn nearby_drivers(
    State(state): State<AppState>,
    Query(params): Query<NearbyQuery>,
) -> Result<Json<Vec<NearbyDriver>>, AppError> {
    if params.radius_km <= 0.0 {
        return Err(AppError::BadRequest("radius_km must be positive".into()));
    }

    let min_weight = params.min_weight_kg.unwrap_or(0.0);

    let rows = sqlx::query_as::<_, NearbyDriver>(
        r#"
        SELECT
            driver_id,
            ST_DistanceSphere(
                location,
                ST_SetSRID(ST_MakePoint($1, $2), 4326)
            ) / 1000.0 AS distance_km,
            max_weight_kg,
            status
        FROM driver_locations
        WHERE
            status = 'Available'
            AND max_weight_kg >= $3
            AND ST_DWithin(
                location::geography,
                ST_SetSRID(ST_MakePoint($1, $2), 4326)::geography,
                $4 * 1000
            )
        ORDER BY distance_km ASC, max_weight_kg DESC
        "#,
    )
    .bind(params.lng)
    .bind(params.lat)
    .bind(min_weight)
    .bind(params.radius_km)
    .fetch_all(&state.db)
    .await?;

    Ok(Json(rows))
}

// ---------------------------------------------------------------------------
// Update driver location (called by Tracking Service every 5–10 s)
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
pub struct UpdateLocationRequest {
    pub lat: f64,
    pub lng: f64,
    /// DriverStatus string: "Available" | "Busy" | "Offline" | "Suspended"
    pub status: String,
    pub max_weight_kg: f64,
}

pub async fn update_driver_location(
    State(state): State<AppState>,
    Path(driver_id): Path<Uuid>,
    Json(body): Json<UpdateLocationRequest>,
) -> Result<StatusCode, AppError> {
    sqlx::query(
        r#"
        INSERT INTO driver_locations (driver_id, location, status, max_weight_kg, updated_at)
        VALUES (
            $1,
            ST_SetSRID(ST_MakePoint($2, $3), 4326),
            $4,
            $5,
            $6
        )
        ON CONFLICT (driver_id) DO UPDATE SET
            location      = EXCLUDED.location,
            status        = EXCLUDED.status,
            max_weight_kg = EXCLUDED.max_weight_kg,
            updated_at    = EXCLUDED.updated_at
        "#,
    )
    .bind(driver_id)
    .bind(body.lng)
    .bind(body.lat)
    .bind(&body.status)
    .bind(body.max_weight_kg)
    .bind(Utc::now())
    .execute(&state.db)
    .await?;

    Ok(StatusCode::NO_CONTENT)
}
