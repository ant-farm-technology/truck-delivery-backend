#!/usr/bin/env bash
set -euo pipefail

export $(grep -v '^#' .env | xargs) 2>/dev/null || true

echo "==> Starting truck-delivery-ocr on port ${PORT:-8090}..."
python -m uvicorn ocr.main:create_app \
  --factory \
  --host 0.0.0.0 \
  --port "${PORT:-8090}" \
  --reload
