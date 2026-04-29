from contextlib import asynccontextmanager
from typing import AsyncGenerator

import structlog
from fastapi import FastAPI
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

from optimizer.config import settings
from optimizer.routes.bin_check import router as bin_check_router
from optimizer.routes.health import router as health_router
from optimizer.routes.optimize import router as optimize_router
from optimizer.telemetry import setup_telemetry

logger = structlog.get_logger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    logger.info("optimizer_starting", service=settings.service_name, port=settings.port)
    setup_telemetry()
    yield
    logger.info("optimizer_shutdown")


def create_app() -> FastAPI:
    app = FastAPI(
        title="Truck Delivery Optimizer",
        description="Stateless VRP optimization service (OR-Tools + greedy fallback)",
        version="0.1.0",
        lifespan=lifespan,
    )

    FastAPIInstrumentor.instrument_app(app)

    app.include_router(health_router)
    app.include_router(optimize_router)
    app.include_router(bin_check_router)

    return app
