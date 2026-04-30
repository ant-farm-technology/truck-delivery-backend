import threading
from contextlib import asynccontextmanager
from typing import AsyncGenerator

import structlog
from fastapi import FastAPI
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

from ocr.config import settings
from ocr.routes.extract import router as extract_router
from ocr.routes.health import router as health_router
from ocr.telemetry import setup_telemetry

logger = structlog.get_logger(__name__)

_consumer_thread: threading.Thread | None = None
_consumer = None


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    global _consumer, _consumer_thread

    logger.info("ocr_service_starting", service=settings.service_name, port=settings.port)
    setup_telemetry()

    # Pre-warm PaddleOCR models on startup to avoid cold start on first request
    from ocr.services.id_card_ocr import IdCardOcrService
    from ocr.services.license_ocr import LicenseOcrService
    from ocr.services.vehicle_reg_ocr import VehicleRegistrationOcrService

    IdCardOcrService.get_instance()
    LicenseOcrService.get_instance()
    VehicleRegistrationOcrService.get_instance()
    logger.info("ocr_models_warmed")

    # Start Kafka consumer in a background daemon thread
    from ocr.consumers.driver_documents_consumer import DriverDocumentsConsumer

    _consumer = DriverDocumentsConsumer()
    _consumer_thread = threading.Thread(target=_consumer.start, daemon=True, name="kafka-consumer")
    _consumer_thread.start()
    logger.info("kafka_consumer_started")

    yield

    if _consumer:
        _consumer.stop()
    logger.info("ocr_service_shutdown")


def create_app() -> FastAPI:
    app = FastAPI(
        title="Truck Delivery OCR Service",
        description="Driver document OCR verification — PaddleOCR + Vietnamese documents",
        version="0.1.0",
        lifespan=lifespan,
    )

    FastAPIInstrumentor.instrument_app(app)

    app.include_router(health_router)
    app.include_router(extract_router)

    return app
