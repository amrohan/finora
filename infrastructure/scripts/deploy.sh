#!/usr/bin/env bash
set -euo pipefail

# Finora Production Automated Deployment Script
# Manages zero-downtime container updates, EF Core schema migrations, and health validation.

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${PROJECT_ROOT}"

echo "=========================================================="
echo "🚀 Starting Finora Production Deployment"
echo "Timestamp: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "Directory: ${PROJECT_ROOT}"
echo "=========================================================="

# 1. Check for required environment variables
if [ ! -f ".env" ]; then
  echo "Error: .env file not found in ${PROJECT_ROOT}." >&2
  echo "Copy .env.example to .env and configure all required secrets." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source .env
set +a

# 2. Build Frontend Production PWA Assets
echo "📦 Building Angular SPA & PWA bundle..."
(cd frontend && npm ci && npm run build)

# 3. Apply EF Core Database Migrations
echo "🗄️ Applying EF Core Database Migrations..."
if command -v dotnet &> /dev/null; then
  dotnet ef database update \
    --project backend/src/Finora.Infrastructure \
    --startup-project backend/src/Finora.Api \
    --connection "${DATABASE_CONNECTION_STRING}"
else
  echo "Notice: dotnet CLI not found on host. Ensure migrations are applied via bundle or container."
fi

# 4. Build & Restart Docker Containers Gracefully
echo "🐳 Building and spinning up application containers..."
docker compose build --pull api
docker compose up -d --remove-orphans

# 5. Verify Health Endpoint
echo "🩺 Waiting for API health check..."
ATTEMPTS=0
MAX_ATTEMPTS=12
HEALTHY=false

while [ "${ATTEMPTS}" -lt "${MAX_ATTEMPTS}" ]; do
  if curl -sf http://localhost:5000/health > /dev/null 2>&1 || curl -sf http://localhost/health > /dev/null 2>&1; then
    HEALTHY=true
    break
  fi
  ATTEMPTS=$((ATTEMPTS + 1))
  echo "Health check attempt ${ATTEMPTS}/${MAX_ATTEMPTS}... retrying in 5s"
  sleep 5
done

if [ "${HEALTHY}" = true ]; then
  echo "=========================================================="
  echo "✅ Finora deployment succeeded! Services are healthy."
  echo "=========================================================="
else
  echo "=========================================================="
  echo "❌ Error: Deployment failed health check validation." >&2
  echo "Checking container logs:" >&2
  docker compose logs api --tail=50 >&2
  echo "=========================================================="
  exit 1
fi

