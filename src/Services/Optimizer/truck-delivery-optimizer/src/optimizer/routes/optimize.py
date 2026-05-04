import asyncio
from functools import partial

import structlog
from fastapi import APIRouter, HTTPException, Request


from optimizer.models.request import OptimizeRequest
from optimizer.models.response import OptimizeResponse
from optimizer.solver.vrp_solver import VrpSolver
from optimizer.telemetry import get_tracer

router = APIRouter(tags=["optimize"])
logger = structlog.get_logger(__name__)
_solver = VrpSolver()


@router.post("/optimize", response_model=OptimizeResponse)
async def optimize(request: OptimizeRequest, http_request: Request) -> OptimizeResponse:
    correlation_id = http_request.headers.get("x-correlation-id", request.correlation_id or "")
    log = logger.bind(
        correlation_id=correlation_id,
        num_drivers=len(request.drivers),
        num_orders=len(request.orders),
    )

    tracer = get_tracer()
    with tracer.start_as_current_span("optimize") as span:
        # Filter ineligible drivers by license grade before running VRP
        if request.required_license_grades:
            allowed = {g.upper() for g in request.required_license_grades}
            eligible = [
                d for d in request.drivers
                if d.license_grade is not None and d.license_grade.upper() in allowed
            ]
            if not eligible:
                raise HTTPException(
                    status_code=422,
                    detail=f"No eligible drivers for required license grades: {request.required_license_grades}",
                )
            if len(eligible) < len(request.drivers):
                log.info(
                    "license_grade_filter_applied",
                    original=len(request.drivers),
                    eligible=len(eligible),
                    required_grades=request.required_license_grades,
                )
            request = request.model_copy(update={"drivers": eligible})

        span.set_attribute("optimizer.num_drivers", len(request.drivers))
        span.set_attribute("optimizer.num_orders", len(request.orders))
        span.set_attribute("optimizer.correlation_id", correlation_id)

        log.info("optimization_requested")

        try:
            # Run CPU-bound solver in thread pool to avoid blocking event loop
            loop = asyncio.get_event_loop()
            response = await loop.run_in_executor(None, partial(_solver.solve, request))
        except Exception as exc:
            log.error("optimization_failed", error=str(exc))
            raise HTTPException(status_code=500, detail=f"Solver error: {exc}") from exc

        span.set_attribute("optimizer.strategy_used", response.strategy_used)
        span.set_attribute("optimizer.solve_time_ms", response.solve_time_ms)
        span.set_attribute("optimizer.unassigned_count", len(response.unassigned_order_ids))

        log.info(
            "optimization_completed",
            strategy=response.strategy_used,
            solve_time_ms=response.solve_time_ms,
            unassigned=len(response.unassigned_order_ids),
        )

        return response
