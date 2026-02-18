#!/usr/bin/env bash
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

quality_gate() {
  echo "[gate] scanning for commercial-release markers in production code"

  local warnings=0
  local prod_paths=(ApiTester.Web ApiTester.Site ApiTester.Ui ApiTester.McpServer ApiTester.Cli ApiTester.AI ApiTester.Rag)
  local ui_paths=(ApiTester.Site/Components/Pages ApiTester.Ui/Pages)

  local patterns=(
    "NotImplementedException"
    "TODO:"
    "placeholder"
    "stub"
    "hack"
    "temp"
  )

  for pattern in "${patterns[@]}"; do
    local target_paths=("${prod_paths[@]}")
    if [[ "$pattern" == "placeholder" ]]; then
      target_paths=("${ui_paths[@]}")
    fi

    if rg -n -i "$pattern" "${target_paths[@]}" --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/Test*/**' --glob '!**/*.md'; then
      echo "[gate][WARN] Found '$pattern' marker in production paths (non-blocking)."
      warnings=1
    fi
  done

  if [[ "$warnings" -ne 0 ]]; then
    echo "[gate][WARN] quality gate completed with warnings"
  else
    echo "[gate] quality gate passed"
  fi
}

run_step() {
  local label="$1"
  shift
  echo "[build] $label"
  "$@"
  local code=$?
  if [[ $code -ne 0 ]]; then
    echo "[build][WARN] '$label' failed with exit code $code"
  fi
  return $code
}

run_step "dotnet restore" dotnet restore
restore_code=$?
if [[ $restore_code -ne 0 ]]; then
  echo "[build][WARN] restore failed; possible proxy/NU1301/403 environment issue. Continuing without restore retry."
fi

run_step "dotnet build -c Release --no-restore" dotnet build -c Release --no-restore
build_code=$?

run_step "dotnet test -c Release --no-build --no-restore --logger trx" dotnet test -c Release --no-build --no-restore --logger trx
test_code=$?

quality_gate

if [[ "${1:-}" == "--docker" ]]; then
  run_step "docker build (web/site/smokeapi-if-present)" docker build -f ApiTester.Web/Dockerfile -t apitester-web:local .
  run_step "docker build (site)" docker build -f ApiTester.Site/Dockerfile -t apitester-site:local .
  if [[ -f ApiTester.SmokeApi/Dockerfile ]]; then
    run_step "docker build (smokeapi)" docker build -f ApiTester.SmokeApi/Dockerfile -t apitester-smokeapi:local .
  fi
fi

if [[ $build_code -ne 0 || $test_code -ne 0 ]]; then
  exit 1
fi

exit 0
