#!/usr/bin/env bash
set -euo pipefail

echo "==> Setting up truck-delivery-ocr..."

if ! command -v python3 &>/dev/null; then
  echo "ERROR: python3 not found" && exit 1
fi

if ! command -v uv &>/dev/null; then
  echo "==> Installing uv..."
  pip install uv
fi

echo "==> Installing dependencies..."
uv pip install -e ".[dev]"

if [ ! -f .env ]; then
  cp .env.example .env
  echo "==> Created .env from .env.example (review and update values)"
fi

echo "==> Pre-warming PaddleOCR Vietnamese models (first run ~1GB download)..."
python -c "
from paddleocr import PaddleOCR
print('Downloading detection model...')
PaddleOCR(use_angle_cls=True, lang='vi', show_log=False)
print('Models ready.')
"

echo "==> Setup complete. Run: ./run.sh"
