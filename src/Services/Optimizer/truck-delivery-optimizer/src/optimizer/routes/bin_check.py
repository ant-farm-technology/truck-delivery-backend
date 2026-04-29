import asyncio
from functools import partial

import structlog
from fastapi import APIRouter, HTTPException, Request

from optimizer.models.request import BinCheckRequest
from optimizer.models.response import BinCheckResponse
from optimizer.solver.bin_packing_solver import BinPackingSolver
from optimizer.telemetry import get_tracer

router = APIRouter(tags=["bin-check"])
logger = structlog.get_logger(__name__)
_solver = BinPackingSolver()


@router.post("/bin-check", response_model=BinCheckResponse)
async def bin_check(request: BinCheckRequest, http_request: Request) -> BinCheckResponse:
    correlation_id = http_request.headers.get("x-correlation-id", "")
    log = logger.bind(
        correlation_id=correlation_id,
        num_packages=len(request.packages),
        truck_volume_m3=round(
            request.truck.length_m * request.truck.width_m * request.truck.height_m, 2
        ),
    )

    tracer = get_tracer()
    with tracer.start_as_current_span("bin_check") as span:
        span.set_attribute("bin_check.num_packages", len(request.packages))
        span.set_attribute("bin_check.truck_max_weight_kg", request.truck.max_weight_kg)
        span.set_attribute("bin_check.correlation_id", correlation_id)

        log.info("bin_check_requested")

        try:
            loop = asyncio.get_event_loop()
            response = await loop.run_in_executor(None, partial(_solver.check, request))
        except Exception as exc:
            log.error("bin_check_failed", error=str(exc))
            raise HTTPException(status_code=500, detail=f"Solver error: {exc}") from exc

        span.set_attribute("bin_check.all_fit", response.all_fit)
        span.set_attribute("bin_check.rejected_count", len(response.rejected_packages))
        span.set_attribute("bin_check.requires_tilt", response.requires_tilt)
        span.set_attribute("bin_check.weight_utilization_pct", response.weight_utilization_pct)

        log.info(
            "bin_check_completed",
            all_fit=response.all_fit,
            rejected=len(response.rejected_packages),
            requires_tilt=response.requires_tilt,
            weight_pct=response.weight_utilization_pct,
            volume_pct=response.volume_utilization_pct,
        )

        return response
