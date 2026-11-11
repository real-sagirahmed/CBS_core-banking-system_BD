<#
.SYNOPSIS
    CBS Bangladesh Core Banking System - Windows Deployment Script
.DESCRIPTION
    Automates container orchestration, health verification, and deployment on Docker for Windows / Windows Server.
#>

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Logs
)

$ErrorActionPreference = "Stop"

if ($Down) {
    Write-Host "[INFO] Stopping CBS Core Banking Stack..." -ForegroundColor Yellow
    docker compose down
    exit 0
}

if ($Logs) {
    docker compose logs -f
    exit 0
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " Starting CBS Bangladesh Production Deployment" -ForegroundColor Cyan
Write-Host " Regulation: Bangladesh Bank BRPD Circular No. 12 (13 May 2024)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# Check for .env file
if (-not (Test-Path "docker\.env")) {
    if (Test-Path "docker\.env.example") {
        Write-Host "[INFO] Creating docker\.env from docker\.env.example..." -ForegroundColor Yellow
        Copy-Item "docker\.env.example" "docker\.env"
    }
}

# 1. Build Containers
Write-Host "[1/4] Building production container images..." -ForegroundColor Green
docker compose build

# 2. Start PostgreSQL and Redis
Write-Host "[2/4] Starting PostgreSQL 16 & Redis 7 services..." -ForegroundColor Green
docker compose up -d cbs-db cbs-cache

# 3. Wait for DB Health
Write-Host "[3/4] Waiting for PostgreSQL readiness..." -ForegroundColor Green
$maxRetries = 20
$retries = 0
do {
    Start-Sleep -Seconds 2
    $status = docker inspect -f '{{.State.Health.Status}}' cbs-postgres 2>$null
    $retries++
} while ($status -ne "healthy" -and $retries -lt $maxRetries)

if ($status -ne "healthy") {
    Write-Host "[ERROR] PostgreSQL failed to report healthy status." -ForegroundColor Red
    docker compose logs cbs-db
    exit 1
}

# 4. Start Core API and Proxy
Write-Host "[4/4] Starting CBS Core API & NGINX Proxy..." -ForegroundColor Green
docker compose up -d cbs-api cbs-proxy

Write-Host "Waiting for /health/ready probe..." -ForegroundColor Yellow
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    try {
        $response = Invoke-RestMethod -Uri "http://localhost/health/ready" -TimeoutSec 3 -ErrorAction SilentlyContinue
        if ($response.status -eq "Ready") {
            $healthy = $true
            break
        }
    }
    catch {
        Write-Host "." -NoNewline
    }
}

Write-Host ""
if ($healthy) {
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host " [SUCCESS] CBS Production Stack is fully operational and healthy!" -ForegroundColor Green
    Write-Host " Gateway URL: http://localhost" -ForegroundColor Green
    Write-Host " Health Probe: http://localhost/health/ready" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Health check probe did not respond in time. Checking container logs..." -ForegroundColor Yellow
    docker compose ps
}
