use axum::{extract::State, http::StatusCode, Json};
use serde_json::{json, Value};

use crate::state::AppState;

pub async fn liveness() -> Json<Value> {
    Json(json!({ "status": "ok" }))
}

pub async fn readiness(State(state): State<AppState>) -> (StatusCode, Json<Value>) {
    let db_ok = sqlx::query("SELECT 1").fetch_one(&state.db).await.is_ok();

    let mut redis_conn = state.redis.clone();
    let redis_ok: bool = redis::cmd("PING")
        .query_async::<_, String>(&mut redis_conn)
        .await
        .map(|r| r == "PONG")
        .unwrap_or(false);

    if db_ok && redis_ok {
        (StatusCode::OK, Json(json!({ "status": "ready" })))
    } else {
        (
            StatusCode::SERVICE_UNAVAILABLE,
            Json(json!({
                "status": "not ready",
                "checks": { "database": db_ok, "redis": redis_ok }
            })),
        )
    }
}
