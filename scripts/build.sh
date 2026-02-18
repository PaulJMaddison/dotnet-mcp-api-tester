#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

quality_gate() {
  echo "[gate] scanning for banned placeholders/stubs in production code"

  local failures=0

  if rg -n "NotImplementedException" ApiTester.Web ApiTester.Site ApiTester.Ui ApiTester.McpServer ApiTester.Cli ApiTester.AI ApiTester.Rag --glob '!**/bin/**' --glob '!**/obj/**'; then
    echo "[gate] Found NotImplementedException in production paths."
    failures=1
  fi

  if rg -n "TODO:" ApiTester.Web ApiTester.Site ApiTester.Ui ApiTester.McpServer ApiTester.Cli ApiTester.AI ApiTester.Rag --glob '!**/bin/**' --glob '!**/obj/**'; then
    echo "[gate] Found TODO: marker in production paths."
    failures=1
  fi

  if rg -n "placeholder" ApiTester.Site/Components/Pages ApiTester.Ui/Pages --glob '!**/bin/**' --glob '!**/obj/**'; then
    echo "[gate] Found placeholder marker in production UI routes/pages."
    failures=1
  fi

  if [[ "$failures" -ne 0 ]]; then
    echo "[gate] quality gate failed"
    exit 1
  fi

  echo "[gate] quality gate passed"
}

echo "[build] dotnet restore"
dotnet restore

echo "[build] dotnet build -c Release"
dotnet build -c Release --no-restore

echo "[build] dotnet test -c Release --logger trx"
dotnet test -c Release --no-build --logger trx

quality_gate

if [[ "${1:-}" == "--docker" ]]; then
  echo "[build] docker build (web/site/smokeapi-if-present)"
  docker build -f ApiTester.Web/Dockerfile -t apitester-web:local .
  docker build -f ApiTester.Site/Dockerfile -t apitester-site:local .
  if [[ -f ApiTester.SmokeApi/Dockerfile ]]; then
    docker build -f ApiTester.SmokeApi/Dockerfile -t apitester-smokeapi:local .
  fi
fi
