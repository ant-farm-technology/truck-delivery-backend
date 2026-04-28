#Requires -Version 5.1
<#
.SYNOPSIS
    Run truck-delivery-route locally on Windows.
    Assumes setup.ps1 has already been executed.

.PARAMETER Release
    Run the release build (default: debug).

.PARAMETER Port
    Override server port (default: 8084).
#>
param(
    [switch]$Release,
    [int]$Port = 8084
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------
# Environment — override config.toml defaults for local development
# -----------------------------------------------------------------------
$env:APP__SERVER__PORT            = "$Port"
$env:APP__DATABASE__URL           = "postgres://truckdelivery:changeme@localhost:5432/truck_route"
$env:APP__REDIS__URL              = "redis://localhost:6379"
$env:APP__OTEL__ENDPOINT          = "http://localhost:4317"
$env:RUST_LOG                     = "truck_delivery_route=debug,tower_http=info,sqlx=warn"

Write-Host "==> truck-delivery-route  port=$Port" -ForegroundColor Cyan
Write-Host "    DB  : $env:APP__DATABASE__URL"
Write-Host "    Redis: $env:APP__REDIS__URL"
Write-Host "    OTel : $env:APP__OTEL__ENDPOINT  (disabled if Tempo not running)"
Write-Host ""

if ($Release) {
    $binary = ".\target\release\truck-delivery-route.exe"
    if (-not (Test-Path $binary)) {
        Write-Error "Release binary not found. Run .\setup.ps1 first."
        exit 1
    }
    & $binary
} else {
    cargo run
}
