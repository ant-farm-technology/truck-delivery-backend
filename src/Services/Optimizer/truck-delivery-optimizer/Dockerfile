FROM python:3.12-slim AS builder

WORKDIR /app

RUN pip install uv

COPY pyproject.toml .
RUN uv pip install --system --no-cache-dir -e ".[dev]"

COPY src/ src/

# ---

FROM python:3.12-slim AS runtime

WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=builder /usr/local/lib/python3.12/site-packages /usr/local/lib/python3.12/site-packages
COPY --from=builder /app/src /app/src

USER appuser

EXPOSE 8085

CMD ["uvicorn", "optimizer.main:create_app", "--factory", "--host", "0.0.0.0", "--port", "8085"]
