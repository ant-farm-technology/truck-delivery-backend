#!/usr/bin/env bash
# Setup script for truck-delivery-optimizer (Python / FastAPI / OR-Tools) on Linux / macOS.
# Checks Python 3.12+, creates a virtual environment, and installs all dependencies.

set -euo pipefail

GREEN='\033[0;32m'; CYAN='\033[0;36m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'

step()  { echo -e "\n${CYAN}==> $*${NC}"; }
ok()    { echo -e "  ${GREEN}[OK]${NC} $*"; }
warn()  { echo -e "  ${YELLOW}[WARN]${NC} $*"; }
die()   { echo -e "  ${RED}[ERROR]${NC} $*"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# -----------------------------------------------------------------------
# 1. Python 3.12+
# -----------------------------------------------------------------------
step "Checking Python 3.12+"

PYTHON_CMD=""
for cmd in python3.12 python3 python; do
    if command -v "$cmd" &>/dev/null; then
        ver=$("$cmd" -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')")
        major="${ver%%.*}"; minor="${ver##*.}"
        if [[ "$major" -gt 3 ]] || [[ "$major" -eq 3 && "$minor" -ge 12 ]]; then
            PYTHON_CMD="$cmd"
            break
        fi
    fi
done

[[ -z "$PYTHON_CMD" ]] && die "Python 3.12+ required. Install from https://python.org/downloads/"
ok "Python $("$PYTHON_CMD" --version) at $(command -v "$PYTHON_CMD")"

# -----------------------------------------------------------------------
# 2. Virtual environment
# -----------------------------------------------------------------------
step "Creating virtual environment (.venv)"

if [[ ! -d ".venv" ]]; then
    "$PYTHON_CMD" -m venv .venv
    ok "Created .venv"
else
    ok ".venv already exists"
fi

# shellcheck disable=SC1091
source .venv/bin/activate

# -----------------------------------------------------------------------
# 3. Dependencies
# -----------------------------------------------------------------------
step "Installing dependencies (production + dev)"

pip install --quiet --upgrade pip
pip install --quiet -e ".[dev]"
ok "Dependencies installed"

# -----------------------------------------------------------------------
# 4. .env file
# -----------------------------------------------------------------------
step "Checking .env file"

if [[ ! -f ".env" ]]; then
    cp .env.example .env
    ok "Created .env from .env.example (edit as needed)"
else
    ok ".env already exists"
fi

echo ""
ok "Setup complete. Run with: ${CYAN}./run.sh${NC}"
