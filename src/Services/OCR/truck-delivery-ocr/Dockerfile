FROM python:3.12-slim AS builder

WORKDIR /app

RUN apt-get update && apt-get install -y \
    libgomp1 libglib2.0-0 libsm6 libxext6 libxrender-dev \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir uv

COPY pyproject.toml .
RUN uv pip install --system --no-cache-dir -e .

# Pre-download PaddleOCR Vietnamese models into /app/.paddleocr
ENV HOME=/app
RUN python -c "from paddleocr import PaddleOCR; PaddleOCR(use_angle_cls=True, lang='vi', show_log=False)" \
    && echo "PaddleOCR models downloaded"

COPY src/ src/

# ---

FROM python:3.12-slim AS runtime

RUN apt-get update && apt-get install -y \
    libgomp1 libglib2.0-0 libsm6 libxext6 libxrender-dev \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -r appgroup && useradd -r -g appgroup -m appuser

WORKDIR /app

COPY --from=builder /usr/local/lib/python3.12/site-packages /usr/local/lib/python3.12/site-packages
COPY --from=builder /app/.paddleocr /home/appuser/.paddleocr
COPY --from=builder /app/src /app/src

RUN chown -R appuser:appgroup /home/appuser/.paddleocr

USER appuser

ENV PORT=8090
EXPOSE 8090

HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8090/health')" || exit 1

CMD ["python", "-m", "uvicorn", "ocr.main:create_app", \
     "--factory", "--host", "0.0.0.0", "--port", "8090"]
