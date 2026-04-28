#!/usr/bin/env bash
# Setup script for truck-delivery-route (Rust / axum) on Linux / macOS.
# Installs Rust toolchain if missing, starts infrastructure via Docker,
# and builds a release binary.

set -euo pipefail

GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; NC='\033[0m'

step()  { echo -e "\n${CYAN}==> $*${NC}"; }
ok()    { echo -e "  ${GREEN}[OK]${NC} $*"; }
warn()  { echo -e "  ${YELLOW}[WARN]${NC} $*"; }

# -----------------------------------------------------------------------
# 1. Rust / cargo
# -----------------------------------------------------------------------
step "Checking Rust toolchain"

if ! command -v cargo &>/dev/null; then
    warn "cargo not found — installing via rustup"
    curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain stable
    # shellcheck disable=SC1091
    source "$HOME/.cargo/env"
fi

ok "cargo: $(cargo --version)"

# -----------------------------------------------------------------------
# 2. Docker — optional infrastructure (PostGIS + Redis)
# -----------------------------------------------------------------------
step "Checking Docker (required for PostGIS + Redis)"

if command -v docker &>/dev/null; then
    ok "docker found"

    if ! docker ps --filter "name=route-postgis" --format "{{.Names}}" | grep -q route-postgis; then
        step "Starting PostGIS container"
        docker run -d \
            --name route-postgis \
            -e POSTGRES_USER=truckdelivery \
            -e POSTGRES_PASSWORD=changeme \
            -e POSTGRES_DB=truck_route \
            -p 5432:5432 \
            postgis/postgis:16-3.4
        ok "PostGIS started (port 5432)"
    else
        ok "PostGIS already running"
    fi

    if ! docker ps --filter "name=route-redis" --format "{{.Names}}" | grep -q route-redis; then
        step "Starting Redis container"
        docker run -d \
            --name route-redis \
            -p 6379:6379 \
            redis:7-alpine
        ok "Redis started (port 6379)"
    else
        ok "Redis already running"
    fi

    echo "  Waiting 3s for containers to initialise..."
    sleep 3
else
    warn "docker not found — ensure PostGIS (:5432) and Redis (:6379) are running manually"
fi

# -----------------------------------------------------------------------
# 3. Build release binary
# -----------------------------------------------------------------------
step "Building release binary (cargo build --release)"
cargo build --release

ok "Binary: target/release/truck-delivery-route"
echo -e "\n  Run with: ${CYAN}./run.sh${NC}"
