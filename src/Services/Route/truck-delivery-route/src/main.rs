use axum::{
    routing::{get, post, put},
    Router,
};
use axum_prometheus::PrometheusMetricLayer;
use opentelemetry_otlp::WithExportConfig;
use opentelemetry_sdk::runtime;
use std::net::SocketAddr;
use tower_http::trace::TraceLayer;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};

mod cache;
mod config;
mod db;
mod domain;
mod error;
mod handlers;
mod state;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let settings = config::Settings::load()?;

    init_tracing(&settings.otel.endpoint)?;

    let pool = db::create_pool(&settings.database.url).await?;
    db::run_migrations(&pool).await?;
    tracing::info!("migrations applied");

    let redis_client = redis::Client::open(settings.redis.url.as_str())?;
    let redis_conn = redis::aio::ConnectionManager::new(redis_client).await?;
    tracing::info!("redis connected");

    let app_state = state::AppState { db: pool, redis: redis_conn };

    let (prometheus_layer, metric_handle) = PrometheusMetricLayer::pair();

    let app = Router::new()
        .route("/health", get(handlers::health::liveness))
        .route("/ready", get(handlers::health::readiness))
        .route("/route", get(handlers::route::calculate_route))
        .route("/matrix", post(handlers::matrix::build_matrix))
        .route("/nearby-drivers", get(handlers::drivers::nearby_drivers))
        .route("/drivers/:id/location", put(handlers::drivers::update_driver_location))
        .route("/metrics", get(move || async move { metric_handle.render() }))
        .layer(prometheus_layer)
        .layer(TraceLayer::new_for_http())
        .with_state(app_state);

    let addr: SocketAddr = format!("0.0.0.0:{}", settings.server.port).parse()?;
    tracing::info!(address = %addr, "route service starting");

    let listener = tokio::net::TcpListener::bind(addr).await?;
    axum::serve(listener, app).await?;

    opentelemetry::global::shutdown_tracer_provider();
    Ok(())
}

fn init_tracing(otlp_endpoint: &str) -> anyhow::Result<()> {
    let tracer = opentelemetry_otlp::new_pipeline()
        .tracing()
        .with_exporter(
            opentelemetry_otlp::new_exporter()
                .tonic()
                .with_endpoint(otlp_endpoint),
        )
        .install_batch(runtime::Tokio)?;

    let otel_layer = tracing_opentelemetry::layer().with_tracer(tracer);

    tracing_subscriber::registry()
        .with(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| "truck_delivery_route=info,tower_http=info".into()),
        )
        .with(tracing_subscriber::fmt::layer().json())
        .with(otel_layer)
        .init();

    Ok(())
}
