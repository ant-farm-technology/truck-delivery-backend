from fastapi import APIRouter
from fastapi.responses import JSONResponse

router = APIRouter(tags=["health"])

_ready = True


@router.get("/health")
async def liveness() -> dict:
    return {"status": "healthy"}


@router.get("/ready")
async def readiness() -> JSONResponse:
    if not _ready:
        return JSONResponse(status_code=503, content={"status": "not ready"})
    return JSONResponse(status_code=200, content={"status": "ready"})
