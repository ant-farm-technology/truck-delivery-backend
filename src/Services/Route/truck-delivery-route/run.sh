#!/usr/bin/env bash
# Run truck-delivery-route locally on Linux / macOS.
# Assumes setup.sh has already been executed.
#
# Usage:
#   ./run.sh              # cargo run (debug)
#   ./run.sh --release    # prebuilt release binary
#   ./run.sh --port 9000  # override port

set -euo pipefail

CYAN='\033[0;36m'; NC='\033[0m'
RELEASE=false
PORT=8084

while [[ $# -gt 0 ]]; do
    case "$1" in
        --release) RELEASE=true; shift ;;
        --port)    PORT="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# -----------------------------------------------------------------------
# Environment — override config.toml defaults for local development
# -----------------------------------------------------------------------
export APP__SERVER__PORT="$PORT"
export APP__DATABASE__URL="postgres://truckdelivery:changeme@localhost:5432/truck_route"
export APP__REDIS__URL="redis://localhost:6379"
export APP__OTEL__ENDPOINT="http://localhost:4317"
export RUST_LOG="truck_delivery_route=debug,tower_http=info,sqlx=warn"

echo -e "${CYAN}==> truck-delivery-route  port=${PORT}${NC}"
echo "    DB   : $APP__DATABASE__URL"
echo "    Redis: $APP__REDIS__URL"
echo "    OTel : $APP__OTEL__ENDPOINT  (disabled if Tempo not running)"
echo ""

if [[ "$RELEASE" == true ]]; then
    BINARY="./target/release/truck-delivery-route"
    if [[ ! -f "$BINARY" ]]; then
        echo "Release binary not found. Run ./setup.sh first."
        exit 1
    fi
    exec "$BINARY"
else
    exec cargo run
fi
