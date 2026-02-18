#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"
SMOKE_API_KEY="${SMOKE_API_KEY:-dev-local-key}"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/docker-compose.yml}"
ARTIFACT_DIR="$ROOT_DIR/artifacts/smoke"
EVIDENCE_ZIP="$ARTIFACT_DIR/evidence.zip"

mkdir -p "$ARTIFACT_DIR"

cleanup() {
  status=$?
  if [ $status -ne 0 ]; then
    echo "Smoke test failed. Capturing compose logs..." >&2
    docker compose -f "$COMPOSE_FILE" logs --no-color > "$ARTIFACT_DIR/compose.log" || true
    echo "Logs written to $ARTIFACT_DIR/compose.log" >&2
  fi

  docker compose -f "$COMPOSE_FILE" down -v --remove-orphans || true
  exit $status
}
trap cleanup EXIT

docker compose -f "$COMPOSE_FILE" up -d --build

wait_for_health() {
  local service="$1"
  local timeout_seconds=180
  local elapsed=0
  while [ $elapsed -lt $timeout_seconds ]; do
    status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$service" 2>/dev/null || true)"
    if [ "$status" = "healthy" ] || [ "$status" = "running" ]; then
      return 0
    fi
    sleep 3
    elapsed=$((elapsed + 3))
  done

  echo "Timed out waiting for $service health." >&2
  return 1
}

wait_for_health apitester-sqlserver
wait_for_health apitester-fixture-api
wait_for_health apitester-web
wait_for_health apitester-site

headers=(-H "X-Api-Key: $SMOKE_API_KEY" -H "Content-Type: application/json")

project_json="$(curl -fsS "${headers[@]}" -d '{"name":"smoke-project"}' "$API_BASE_URL/api/projects")"
project_id="$(python3 -c 'import json,sys; print(json.loads(sys.stdin.read())["projectId"])' <<< "$project_json")"

curl -fsS -H "X-Api-Key: $SMOKE_API_KEY" -F "file=@$ROOT_DIR/tests/fixtures/petstore-small.json;type=application/json" \
  "$API_BASE_URL/api/projects/$project_id/openapi/import" > /dev/null

run_json="$(curl -fsS -X POST -H "X-Api-Key: $SMOKE_API_KEY" "$API_BASE_URL/api/projects/$project_id/runs/execute/listPets")"
run_id="$(python3 -c 'import json,sys; print(json.loads(sys.stdin.read())["runId"])' <<< "$run_json")"

curl -fsS -H "X-Api-Key: $SMOKE_API_KEY" "$API_BASE_URL/runs/$run_id/export/evidence-bundle" -o "$EVIDENCE_ZIP"

entries="$(unzip -l "$EVIDENCE_ZIP")"
if ! rg -q "manifest.json" <<< "$entries"; then
  echo "manifest.json missing from evidence bundle." >&2
  exit 1
fi
if ! rg -q "run.json" <<< "$entries"; then
  echo "run.json missing from evidence bundle." >&2
  exit 1
fi

echo "Smoke test passed. Evidence bundle saved at $EVIDENCE_ZIP"
