FROM python:3.12-slim AS builder

WORKDIR /app

RUN apt-get update && apt-get install -y \
    libgomp1 libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir uv

COPY pyproject.toml .
COPY src/ src/
RUN uv pip install --system --no-cache-dir .

# Pre-download PaddleOCR v3 (paddlex-based) Vietnamese models.
# PaddleOCR v3 stores models in $HOME/.paddlex, not .paddleocr
ENV HOME=/app
RUN python -c "from paddleocr import PaddleOCR; PaddleOCR(use_textline_orientation=True, lang='vi')" \
    && echo "PaddleOCR models downloaded"

# ---

FROM python:3.12-slim AS runtime

RUN apt-get update && apt-get install -y \
    libgomp1 libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -r appgroup && useradd -r -g appgroup -m appuser

WORKDIR /app

COPY --from=builder /usr/local/lib/python3.12/site-packages /usr/local/lib/python3.12/site-packages
COPY --from=builder /app/.paddlex /home/appuser/.paddlex

RUN chown -R appuser:appgroup /home/appuser/.paddlex

USER appuser

ENV PORT=8090
EXPOSE 8090

HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8090/health')" || exit 1

CMD ["python", "-m", "uvicorn", "ocr.main:create_app", \
     "--factory", "--host", "0.0.0.0", "--port", "8090"]
