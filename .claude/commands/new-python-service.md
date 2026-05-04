# /new-python-service — Scaffold Python Microservice (OR-Tools / Routing)

Scaffold một Python microservice cho routing optimization với Google OR-Tools.

**Service name:** $ARGUMENTS

## Yêu cầu

Tạo project structure sau:

```
src/Services/$ARGUMENTS/
  pyproject.toml        # uv-managed dependencies
  uv.lock
  Dockerfile
  .env.example
  src/
    $ARGUMENTS/
      __init__.py
      main.py           # FastAPI app factory
      config.py         # Settings từ env vars (pydantic-settings)
      routes/
        __init__.py
        health.py       # /health và /ready
        {domain}.py     # domain-specific routes
      solver/
        __init__.py
        vrp_solver.py   # Vehicle Routing Problem solver
        models.py       # OR-Tools input/output models
      models/
        __init__.py
        request.py      # Pydantic request models
        response.py     # Pydantic response models
      telemetry.py      # OpenTelemetry setup
  tests/
    __init__.py
    test_{domain}.py
```

### pyproject.toml dependencies bắt buộc:
```toml
[project]
requires-python = ">=3.12"
dependencies = [
    "fastapi>=0.115",
    "uvicorn[standard]>=0.32",
    "pydantic>=2.9",
    "pydantic-settings>=2.6",
    "ortools>=9.11",
    "opentelemetry-sdk>=1.28",
    "opentelemetry-exporter-otlp>=1.28",
    "opentelemetry-instrumentation-fastapi>=0.49b0",
    "structlog>=24.4",
    "httpx>=0.27",
]
```

### main.py pattern:
```python
from contextlib import asynccontextmanager
from fastapi import FastAPI
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

@asynccontextmanager
async def lifespan(app: FastAPI):
    # startup: init telemetry, warm up solver
    yield
    # shutdown: cleanup

def create_app() -> FastAPI:
    app = FastAPI(lifespan=lifespan)
    FastAPIInstrumentor.instrument_app(app)
    app.include_router(health_router)
    app.include_router(domain_router)
    return app
```

### VRP Solver pattern (OR-Tools):
```python
from ortools.constraint_solver import routing_enums_pb2
from ortools.constraint_solver import pywrapcp

class VrpSolver:
    def solve(self, data: VrpInput) -> VrpSolution:
        manager = pywrapcp.RoutingIndexManager(...)
        routing = pywrapcp.RoutingModel(manager)
        # Define constraints: distance, capacity, time windows
        # Set search parameters
        # Solve và return solution
```

### Pydantic models:
```python
from pydantic import BaseModel, Field

class VrpInput(BaseModel):
    num_vehicles: int = Field(gt=0)
    depot_index: int = Field(ge=0)
    distance_matrix: list[list[int]]
    # ...

class VrpSolution(BaseModel):
    routes: list[list[int]]
    total_distance: int
    solve_time_ms: float
```

### Health endpoints:
- `GET /health` — liveness: return `{"status": "healthy"}`
- `GET /ready` — readiness: return 200 hoặc 503

### OpenTelemetry:
- Service name = `$ARGUMENTS`
- Auto-instrument FastAPI với `FastAPIInstrumentor`
- Export to OTLP (env `OTEL_EXPORTER_OTLP_ENDPOINT`)

### Dockerfile: multi-stage với uv
```dockerfile
# Stage 1: Builder (python:3.12-slim + uv)
# Stage 2: Runtime (python:3.12-slim)
# Non-root user
# EXPOSE 8080
# CMD ["uvicorn", "src.$ARGUMENTS.main:create_app", "--factory", "--host", "0.0.0.0", "--port", "8080"]
```

## Rules
- Async endpoints everywhere (`async def`)
- Không dùng `time.sleep` — dùng `asyncio.sleep`
- Pydantic v2 models với strict type hints
- Tất cả solver logic trong `solver/` module, không trong routes
- OR-Tools solve timeout: max 30 giây, configurable qua env
- Response luôn include `solve_time_ms` để monitor performance
