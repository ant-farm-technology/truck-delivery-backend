#!/usr/bin/env bash
# Run truck-delivery-optimizer locally on Linux / macOS.
# Assumes setup.sh has already been executed.
#
# Usage:
#   ./run.sh              # dev mode with --reload
#   ./run.sh --no-reload  # production-like, no file watching
#   ./run.sh --port 9085  # override port

set -euo pipefail

CYAN='\033[0;36m'; NC='\033[0m'
RELOAD=true
PORT=8085

while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-reload) RELOAD=false; shift ;;
        --port)      PORT="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [[ ! -d ".venv" ]]; then
    echo ".venv not found. Run ./setup.sh first."
    exit 1
fi

# shellcheck disable=SC1091
source .venv/bin/activate

# -----------------------------------------------------------------------
# Environment — local dev overrides (OTel disabled by default locally)
# -----------------------------------------------------------------------
export PORT="$PORT"
export LOG_LEVEL="DEBUG"
export OTEL_ENABLED="false"
export SOLVER_TIMEOUT_SECONDS="10"
export SOLVER_FIRST_SOLUTION_STRATEGY="PATH_CHEAPEST_ARC"
export SOLVER_LOCAL_SEARCH_METAHEURISTIC="GUIDED_LOCAL_SEARCH"
export WEIGHT_DISTANCE="1.0"
export WEIGHT_TIME="0.5"
export WEIGHT_PENALTY="1000.0"

echo -e "${CYAN}==> truck-delivery-optimizer  port=${PORT}${NC}"
echo "    OTel : disabled  (set OTEL_ENABLED=true + OTEL_EXPORTER_OTLP_ENDPOINT to enable)"
echo ""

UVICORN_ARGS=(
    -m uvicorn
    optimizer.main:create_app
    --factory
    --host 0.0.0.0
    --port "$PORT"
)

if [[ "$RELOAD" == true ]]; then
    UVICORN_ARGS+=(--reload --reload-dir src)
fi

exec python "${UVICORN_ARGS[@]}"
