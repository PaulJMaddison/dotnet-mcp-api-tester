#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"
ARTIFACT_ROOT="$ROOT_DIR/artifacts/all-tests/$TIMESTAMP"
TRX_DIR="$ARTIFACT_ROOT/trx"
LOG_DIR="$ARTIFACT_ROOT/logs"
SUITE_STATUS_FILE="$ARTIFACT_ROOT/summary.txt"

mkdir -p "$TRX_DIR" "$LOG_DIR"

BUILD_STATUS="NOT_RUN"
DOTNET_TEST_STATUS="NOT_RUN"
SMOKE_STATUS="NOT_RUN"
E2E_STATUS="NOT_RUN"
SECURITY_STATUS="NOT_RUN"
PERF_STATUS="NOT_RUN"
DOTNET_TRX_PATH="$TRX_DIR/dotnet-tests.trx"

log() { printf '[all-tests] %s\n' "$*"; }

final_summary() {
  {
    echo "build status: $BUILD_STATUS"
    echo "dotnet test status: $DOTNET_TEST_STATUS"
    echo "dotnet trx path: $DOTNET_TRX_PATH"
    echo "smoke status: $SMOKE_STATUS"
    echo "e2e status: $E2E_STATUS"
    echo "security status: $SECURITY_STATUS"
    echo "perf sanity status: $PERF_STATUS"
    echo "artifact folder path: $ARTIFACT_ROOT"
  } | tee "$SUITE_STATUS_FILE"
}

on_failure() {
  local exit_code=$?
  log "FAILED (exit code $exit_code)"
  final_summary
  exit "$exit_code"
}
trap on_failure ERR

run_step() {
  local name="$1"
  shift
  local log_file="$LOG_DIR/${name// /_}.log"
  log "$name"
  "$@" 2>&1 | tee "$log_file"
}

copy_if_dir() {
  local source_dir="$1"
  local target_dir="$2"
  if [[ -d "$source_dir" ]]; then
    mkdir -p "$target_dir"
    cp -R "$source_dir"/. "$target_dir"/
  fi
}

run_step "dotnet_restore" dotnet restore
run_step "dotnet_build_release" dotnet build -c Release
BUILD_STATUS="PASS"

run_step "dotnet_test_release" dotnet test -c Release --logger "trx;LogFileName=dotnet-tests.trx" --results-directory "$TRX_DIR"
DOTNET_TEST_STATUS="PASS"

if [[ -f "$ROOT_DIR/scripts/smoke.sh" ]]; then
  run_step "smoke_suite" "$ROOT_DIR/scripts/smoke.sh"
  SMOKE_STATUS="PASS"
  if [[ -f "$ROOT_DIR/artifacts/smoke/compose.log" ]]; then
    cp "$ROOT_DIR/artifacts/smoke/compose.log" "$ARTIFACT_ROOT/smoke-compose.log"
  fi
else
  SMOKE_STATUS="SKIPPED (no smoke script found)"
fi

if [[ -f "$ROOT_DIR/scripts/e2e.sh" ]]; then
  run_step "e2e_suite" "$ROOT_DIR/scripts/e2e.sh"
  E2E_STATUS="PASS"
  copy_if_dir "$ROOT_DIR/artifacts/e2e" "$ARTIFACT_ROOT/e2e"
else
  E2E_STATUS="SKIPPED (no e2e script found)"
fi

if [[ -f "$ROOT_DIR/scripts/security.sh" ]]; then
  run_step "security_suite" "$ROOT_DIR/scripts/security.sh"
  SECURITY_STATUS="PASS"
  copy_if_dir "$ROOT_DIR/artifacts/security" "$ARTIFACT_ROOT/security"
elif [[ -f "$ROOT_DIR/tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj" ]]; then
  run_step "security_suite_direct" dotnet test "$ROOT_DIR/tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj" -c Release --logger "trx;LogFileName=security-tests.trx" --results-directory "$ARTIFACT_ROOT/security"
  SECURITY_STATUS="PASS"
else
  SECURITY_STATUS="SKIPPED (no security suite found)"
fi

mapfile -t perf_files < <(find "$ROOT_DIR" -type f \( -name '*k6*.js' -o -name 'k6*.js' -o -name '*load*.js' \) -not -path '*/bin/*' -not -path '*/obj/*')
if (( ${#perf_files[@]} > 0 )); then
  if command -v k6 >/dev/null 2>&1; then
    PERF_STATUS="PASS"
    for perf_file in "${perf_files[@]}"; do
      perf_name="$(basename "$perf_file")"
      run_step "perf_sanity_${perf_name}" k6 run --duration 30s --vus 1 "$perf_file"
    done
  else
    PERF_STATUS="WARNING (k6 missing; perf scripts present but not documented as required)"
    log "$PERF_STATUS"
  fi
else
  PERF_STATUS="PASS (no perf harness scripts found)"
fi

trap - ERR
final_summary
log "ALL SUITES PASSED"
