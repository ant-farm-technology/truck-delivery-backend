FROM python:3.12-slim AS builder

WORKDIR /app

RUN pip install --no-cache-dir uv

COPY pyproject.toml .
# Install production deps only (no dev/test extras)
RUN uv pip install --system --no-cache-dir -e .

COPY src/ src/

# ---

FROM python:3.12-slim AS runtime

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

WORKDIR /app

COPY --from=builder /usr/local/lib/python3.12/site-packages /usr/local/lib/python3.12/site-packages
COPY --from=builder /app/src /app/src

USER appuser

ENV PORT=8085
ENV PYTHONPATH=/app/src
EXPOSE 8085

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8085/health')" || exit 1

# python -m uvicorn avoids needing the uvicorn script in PATH
CMD ["python", "-m", "uvicorn", "optimizer.main:create_app", \
     "--factory", "--host", "0.0.0.0", "--port", "8085"]
