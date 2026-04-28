#Requires -Version 5.1
<#
.SYNOPSIS
    Setup script for truck-delivery-route (Rust / axum) on Windows.
    Installs Rust toolchain if missing, starts infrastructure via Docker,
    and builds a release binary.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }

# -----------------------------------------------------------------------
# 1. Rust / cargo
# -----------------------------------------------------------------------
Write-Step "Checking Rust toolchain"

if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Warn "cargo not found — downloading rustup-init.exe"
    $rustupUrl = "https://win.rustup.rs/x86_64"
    $installer  = "$env:TEMP\rustup-init.exe"
    Invoke-WebRequest -Uri $rustupUrl -OutFile $installer -UseBasicParsing
    & $installer -y --default-toolchain stable
    Remove-Item $installer -Force

    # Refresh PATH so cargo is available in this session
    $env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"
}

$cargoVersion = cargo --version
Write-Ok "cargo: $cargoVersion"

# -----------------------------------------------------------------------
# 2. Docker — optional infrastructure (PostGIS + Redis)
# -----------------------------------------------------------------------
Write-Step "Checking Docker (required for PostGIS + Redis)"

if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Ok "docker found"

    # PostGIS
    $pgRunning = docker ps --filter "name=route-postgis" --format "{{.Names}}" 2>$null
    if (-not $pgRunning) {
        Write-Step "Starting PostGIS container"
        docker run -d `
            --name route-postgis `
            -e POSTGRES_USER=truckdelivery `
            -e POSTGRES_PASSWORD=changeme `
            -e POSTGRES_DB=truck_route `
            -p 5432:5432 `
            postgis/postgis:16-3.4
        Write-Ok "PostGIS started (port 5432)"
    } else {
        Write-Ok "PostGIS already running"
    }

    # Redis
    $redisRunning = docker ps --filter "name=route-redis" --format "{{.Names}}" 2>$null
    if (-not $redisRunning) {
        Write-Step "Starting Redis container"
        docker run -d `
            --name route-redis `
            -p 6379:6379 `
            redis:7-alpine
        Write-Ok "Redis started (port 6379)"
    } else {
        Write-Ok "Redis already running"
    }

    # Brief wait for containers
    Write-Host "  Waiting 3s for containers to initialise..." -ForegroundColor Gray
    Start-Sleep -Seconds 3
} else {
    Write-Warn "docker not found — make sure PostGIS (:5432) and Redis (:6379) are running manually"
}

# -----------------------------------------------------------------------
# 3. Build release binary
# -----------------------------------------------------------------------
Write-Step "Building release binary (cargo build --release)"
cargo build --release

Write-Ok "Binary: target\release\truck-delivery-route.exe"
Write-Host "`n  Run with: .\run.ps1" -ForegroundColor Cyan
