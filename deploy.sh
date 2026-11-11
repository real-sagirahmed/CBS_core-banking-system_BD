#!/usr/bin/env bash
# ==============================================================================
# CBS Bangladesh Core Banking System - Zero-Downtime Production Deployment Script
# Conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024)
# ==============================================================================

set -euo pipefail

echo "======================================================================"
echo " Starting CBS Bangladesh Production Deployment"
echo " Time: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo " Regulation: Bangladesh Bank BRPD Circular No. 12 (13 May 2024)"
echo "======================================================================"

# Check for .env file
if [ ! -f "docker/.env" ]; then
    if [ -f "docker/.env.example" ]; then
        echo "[INFO] docker/.env not found. Creating from docker/.env.example..."
        cp docker/.env.example docker/.env
    else
        echo "[WARNING] docker/.env not found. Using default docker-compose environment."
    fi
fi

# 1. Pull & Build Containers
echo "[1/4] Building production container images..."
docker compose build --pull

# 2. Start PostgreSQL and Redis first
echo "[2/4] Initializing Database & Cache infrastructure..."
docker compose up -d cbs-db cbs-cache

# 3. Wait for PostgreSQL Readiness
echo "[3/4] Waiting for PostgreSQL to pass health checks..."
until [ "$(docker inspect -f {{.State.Health.Status}} cbs-postgres 2>/dev/null)" = "healthy" ]; do
    printf '.'
    sleep 2
done
echo " PostgreSQL is healthy."

# 4. Start CBS Core API and NGINX Proxy
echo "[4/4] Launching CBS API Engine and NGINX Gateway..."
docker compose up -d cbs-api cbs-proxy

echo "Waiting for API readiness probe..."
sleep 5
for i in {1..30}; do
    if curl -s -f http://localhost/health/ready > /dev/null 2>&1; then
        echo "======================================================================"
        echo " [SUCCESS] CBS Production Stack is fully operational and healthy!"
        echo " Gateway URL: http://localhost"
        echo " Health Probe: http://localhost/health/ready"
        echo "======================================================================"
        exit 0
    fi
    printf '.'
    sleep 2
done

echo "[ERROR] Health check timed out. Inspect container logs with 'docker compose logs'."
exit 1
