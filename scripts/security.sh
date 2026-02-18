#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date -u +"%Y%m%d-%H%M%S")"
OUT_DIR="$ROOT_DIR/artifacts/security/$TIMESTAMP"
mkdir -p "$OUT_DIR"
RESULTS_FILE="$OUT_DIR/security-results.txt"
TRX_FILE="$OUT_DIR/security-tests.trx"

{
  echo "[security] started: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  echo "[security] project: tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj"
  dotnet test "$ROOT_DIR/tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj" -c Release --logger "trx;LogFileName=security-tests.trx" --results-directory "$OUT_DIR"
  echo "[security] status: PASS"
} | tee "$RESULTS_FILE"

echo "[security] results: $RESULTS_FILE"
echo "[security] trx: $TRX_FILE"
