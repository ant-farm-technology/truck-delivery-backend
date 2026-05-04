#Requires -Version 5.1
<#
.SYNOPSIS
    Setup script for truck-delivery-optimizer (Python / FastAPI / OR-Tools) on Windows.
    Checks Python 3.12+, creates a virtual environment, and installs all dependencies.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg) { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }

# -----------------------------------------------------------------------
# 1. Python 3.12+
# -----------------------------------------------------------------------
Write-Step "Checking Python 3.12+"

$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonCmd) {
    $pythonCmd = Get-Command python3 -ErrorAction SilentlyContinue
}
if (-not $pythonCmd) {
    Write-Error "Python 3.12+ is required. Download from https://python.org/downloads/"
    exit 1
}

$pyExe = $pythonCmd.Source
$pyVer = & $pyExe -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')"
$pyMajor = [int]($pyVer.Split('.')[0])
$pyMinor = [int]($pyVer.Split('.')[1])

if ($pyMajor -lt 3 -or ($pyMajor -eq 3 -and $pyMinor -lt 12)) {
    Write-Error "Python 3.12+ required, found $pyVer"
    exit 1
}
Write-Ok "Python $pyVer at $pyExe"

# -----------------------------------------------------------------------
# 2. Virtual environment
# -----------------------------------------------------------------------
Write-Step "Creating virtual environment (.venv)"

if (-not (Test-Path ".venv")) {
    & $pyExe -m venv .venv
    Write-Ok "Created .venv"
}
else {
    Write-Ok ".venv already exists"
}

$pipExe = ".\.venv\Scripts\pip.exe"
$pythonVenv = ".\.venv\Scripts\python.exe"

# -----------------------------------------------------------------------
# 3. Dependencies
# -----------------------------------------------------------------------
Write-Step "Installing dependencies (production + dev)"

& $pipExe install --quiet --upgrade pip
& $pipExe install --quiet -e ".[dev]"
Write-Ok "Dependencies installed"

# -----------------------------------------------------------------------
# 4. .env file
# -----------------------------------------------------------------------
Write-Step "Checking .env file"

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Ok "Created .env from .env.example (edit as needed)"
}
else {
    Write-Ok ".env already exists"
}

Write-Host ""
Write-Ok "Setup complete. Run with: .\run.ps1"
