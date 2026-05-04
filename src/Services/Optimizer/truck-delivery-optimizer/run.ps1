#Requires -Version 5.1
<#
.SYNOPSIS
    Run truck-delivery-optimizer locally on Windows.
    Assumes setup.ps1 has already been executed.

.PARAMETER Port
    Override server port (default: 8085).

.PARAMETER Reload
    Enable uvicorn --reload for development (default: true).

.PARAMETER NoReload
    Disable uvicorn --reload (use for production-like run).
#>
param(
    [int]$Port    = 8085,
    [switch]$NoReload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$venvPython = ".\.venv\Scripts\python.exe"
if (-not (Test-Path $venvPython)) {
    Write-Error ".venv not found. Run .\setup.ps1 first."
    exit 1
}

# -----------------------------------------------------------------------
# Environment — local dev overrides (OTel disabled by default locally)
# -----------------------------------------------------------------------
$env:PORT                           = "$Port"
$env:LOG_LEVEL                      = "DEBUG"
$env:OTEL_ENABLED                   = "false"
$env:SOLVER_TIMEOUT_SECONDS         = "10"
$env:SOLVER_FIRST_SOLUTION_STRATEGY = "PATH_CHEAPEST_ARC"
$env:SOLVER_LOCAL_SEARCH_METAHEURISTIC = "GUIDED_LOCAL_SEARCH"
$env:WEIGHT_DISTANCE                = "1.0"
$env:WEIGHT_TIME                    = "0.5"
$env:WEIGHT_PENALTY                 = "1000.0"

Write-Host "`n==> truck-delivery-optimizer  port=$Port" -ForegroundColor Cyan
Write-Host "    OTel : disabled (set OTEL_ENABLED=true + OTEL_EXPORTER_OTLP_ENDPOINT to enable)"
Write-Host ""

$uvicornArgs = @(
    "-m", "uvicorn",
    "optimizer.main:create_app",
    "--factory",
    "--host", "0.0.0.0",
    "--port", "$Port"
)

if (-not $NoReload) {
    $uvicornArgs += "--reload"
    $uvicornArgs += "--reload-dir"
    $uvicornArgs += "src"
}

& $venvPython @uvicornArgs
