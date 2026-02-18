#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"
ARTIFACT_DIR="${ROOT_DIR}/artifacts/e2e/${TIMESTAMP}"
mkdir -p "${ARTIFACT_DIR}"

COMPOSE_FILE="${ROOT_DIR}/docker-compose.e2e.yml"
SITE_URL="${E2E_BASE_URL:-http://localhost:18081}"
WEB_URL="${E2E_WEB_BASE_URL:-http://localhost:18080}"
FIXTURE_URL="${E2E_FIXTURE_BASE_URL:-http://localhost:18082}"

cleanup() {
  docker compose -f "${COMPOSE_FILE}" logs > "${ARTIFACT_DIR}/compose.log" || true
  docker compose -f "${COMPOSE_FILE}" down -v || true
}
trap cleanup EXIT


wait_for_health_status() {
  local service="$1" retries="${2:-120}"
  local cid
  cid=$(docker compose -f "${COMPOSE_FILE}" ps -q "$service")
  for ((i=1; i<=retries; i++)); do
    local status
    status=$(docker inspect --format="{{.State.Health.Status}}" "$cid" 2>/dev/null || true)
    if [[ "$status" == "healthy" ]]; then
      echo "$service healthy"
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for $service health" >&2
  return 1
}

wait_for() {
  local name="$1" url="$2" retries="${3:-120}"
  for ((i=1; i<=retries; i++)); do
    if curl --fail --silent "$url" >/dev/null; then
      echo "${name} ready"
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for ${name} at ${url}" >&2
  return 1
}

docker compose -f "${COMPOSE_FILE}" up -d --build

wait_for_health_status "sqlserver"
wait_for "fixtureapi" "${FIXTURE_URL}/health"
wait_for "web" "${WEB_URL}/health"
wait_for "site" "${SITE_URL}/health"

export E2E_BASE_URL="${SITE_URL}"
export E2E_WEB_BASE_URL="${WEB_URL}"
export E2E_API_KEY="${E2E_API_KEY:-dev-local-key}"
export E2E_ARTIFACTS_DIR="${ARTIFACT_DIR}"

(dotnet build tests/ApiTester.E2E/ApiTester.E2E.csproj -c Release >/dev/null)
pwsh "${ROOT_DIR}/tests/ApiTester.E2E/bin/Release/net8.0/playwright.ps1" install --with-deps chromium

dotnet test tests/ApiTester.E2E/ApiTester.E2E.csproj -c Release --filter "Category=E2E"
