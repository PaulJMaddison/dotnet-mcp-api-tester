#!/usr/bin/env bash
set -euo pipefail

api_url="${API_URL:-http://localhost:5000}"
ui_url="${UI_URL:-http://localhost:5171}"

health_response="$(curl -fsS "${api_url}/health")"

echo "Health response: ${health_response}"

home_html="$(curl -fsS "${ui_url}/")"

if ! rg -q "<h1>Projects</h1>" <<<"${home_html}"; then
  echo "UI home page did not contain expected heading." >&2
  exit 1
fi

echo "Smoke test passed."
