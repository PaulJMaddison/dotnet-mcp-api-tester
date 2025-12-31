#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$root_dir"

echo "Starting ApiTester.Web..."
dotnet run --project ApiTester.Web --launch-profile "ApiTester.Web" &
web_pid=$!

echo "Starting ApiTester.Ui..."
dotnet run --project ApiTester.Ui --launch-profile "ApiTester.Ui" &
ui_pid=$!

cleanup() {
  echo "Stopping services..."
  kill "$web_pid" "$ui_pid" 2>/dev/null || true
}
trap cleanup EXIT

wait "$web_pid" "$ui_pid"
