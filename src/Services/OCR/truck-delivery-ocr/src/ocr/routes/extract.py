import asyncio
import time
from functools import partial

import structlog
from fastapi import APIRouter, HTTPException, Request
from prometheus_client import Counter, Histogram

from ocr.models.request import ExtractIdCardRequest, ExtractLicenseRequest, ExtractVehicleRegRequest
from ocr.models.response import CCCDExtraction, LicenseExtraction, VehicleRegistrationExtraction
from ocr.services.id_card_ocr import IdCardOcrService
from ocr.services.image_loader import load_image_from_url, image_to_numpy
from ocr.services.license_ocr import LicenseOcrService
from ocr.services.vehicle_reg_ocr import VehicleRegistrationOcrService
from ocr.telemetry import get_tracer

router = APIRouter(prefix="/api/v1/ocr", tags=["ocr"])
logger = structlog.get_logger(__name__)

_extraction_duration = Histogram(
    "ocr_extraction_duration_seconds",
    "OCR extraction duration",
    ["document_type"],
)
_extraction_counter = Counter(
    "ocr_extraction_total",
    "Total OCR extractions",
    ["document_type", "status"],
)


@router.post("/extract/id-card", response_model=CCCDExtraction)
async def extract_id_card(request: ExtractIdCardRequest, http_request: Request) -> CCCDExtraction:
    correlation_id = http_request.headers.get("x-correlation-id", "")
    log = logger.bind(correlation_id=correlation_id, document_type="cccd")
    tracer = get_tracer()

    with tracer.start_as_current_span("ocr.extract.id_card") as span:
        span.set_attribute("ocr.document_type", "cccd")
        span.set_attribute("ocr.correlation_id", correlation_id)

        start = time.monotonic()
        try:
            front_img, back_img = await asyncio.gather(
                load_image_from_url(request.front_url),
                load_image_from_url(request.back_url),
            )
            front_arr = image_to_numpy(front_img)
            back_arr = image_to_numpy(back_img)

            svc = IdCardOcrService.get_instance()
            result = await svc.extract(front_arr, back_arr)

            elapsed = time.monotonic() - start
            _extraction_duration.labels(document_type="cccd").observe(elapsed)
            _extraction_counter.labels(document_type="cccd", status="success").inc()

            log.info("id_card_extracted", confidence=result.confidence, elapsed_s=round(elapsed, 3))
            return result

        except Exception as exc:
            _extraction_counter.labels(document_type="cccd", status="error").inc()
            log.error("id_card_extraction_failed", error=str(exc))
            raise HTTPException(status_code=500, detail=f"OCR extraction failed: {exc}") from exc


@router.post("/extract/license", response_model=LicenseExtraction)
async def extract_license(request: ExtractLicenseRequest, http_request: Request) -> LicenseExtraction:
    correlation_id = http_request.headers.get("x-correlation-id", "")
    log = logger.bind(correlation_id=correlation_id, document_type="license")
    tracer = get_tracer()

    with tracer.start_as_current_span("ocr.extract.license") as span:
        span.set_attribute("ocr.document_type", "license")
        span.set_attribute("ocr.correlation_id", correlation_id)

        start = time.monotonic()
        try:
            front_img, back_img = await asyncio.gather(
                load_image_from_url(request.front_url),
                load_image_from_url(request.back_url),
            )
            front_arr = image_to_numpy(front_img)
            back_arr = image_to_numpy(back_img)

            svc = LicenseOcrService.get_instance()
            result = await svc.extract(front_arr, back_arr)

            elapsed = time.monotonic() - start
            _extraction_duration.labels(document_type="license").observe(elapsed)
            _extraction_counter.labels(document_type="license", status="success").inc()

            log.info("license_extracted", grade=result.license_grade, confidence=result.confidence, elapsed_s=round(elapsed, 3))
            return result

        except Exception as exc:
            _extraction_counter.labels(document_type="license", status="error").inc()
            log.error("license_extraction_failed", error=str(exc))
            raise HTTPException(status_code=500, detail=f"OCR extraction failed: {exc}") from exc


@router.post("/extract/vehicle-reg", response_model=VehicleRegistrationExtraction)
async def extract_vehicle_reg(request: ExtractVehicleRegRequest, http_request: Request) -> VehicleRegistrationExtraction:
    correlation_id = http_request.headers.get("x-correlation-id", "")
    log = logger.bind(correlation_id=correlation_id, document_type="vehicle_reg")
    tracer = get_tracer()

    with tracer.start_as_current_span("ocr.extract.vehicle_reg") as span:
        span.set_attribute("ocr.document_type", "vehicle_reg")
        span.set_attribute("ocr.correlation_id", correlation_id)

        start = time.monotonic()
        try:
            front_img, back_img = await asyncio.gather(
                load_image_from_url(request.front_url),
                load_image_from_url(request.back_url),
            )
            front_arr = image_to_numpy(front_img)
            back_arr = image_to_numpy(back_img)

            svc = VehicleRegistrationOcrService.get_instance()
            result = await svc.extract(front_arr, back_arr)

            elapsed = time.monotonic() - start
            _extraction_duration.labels(document_type="vehicle_reg").observe(elapsed)
            _extraction_counter.labels(document_type="vehicle_reg", status="success").inc()

            log.info("vehicle_reg_extracted", plate=result.license_plate, confidence=result.confidence, elapsed_s=round(elapsed, 3))
            return result

        except Exception as exc:
            _extraction_counter.labels(document_type="vehicle_reg", status="error").inc()
            log.error("vehicle_reg_extraction_failed", error=str(exc))
            raise HTTPException(status_code=500, detail=f"OCR extraction failed: {exc}") from exc
