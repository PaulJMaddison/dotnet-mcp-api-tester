#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION_PATH="$ROOT_DIR/DotnetMcpApiTester.sln"
ARTIFACT_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/apitester-build-XXXXXX")"
WEB_PUBLISH_DIR="$ARTIFACT_ROOT/web"
SITE_PUBLISH_DIR="$ARTIFACT_ROOT/site"

cd "$ROOT_DIR"

echo "[build] restore (Release pipeline)"
dotnet restore "$SOLUTION_PATH"

echo "[build] build (Release)"
dotnet build "$SOLUTION_PATH" -c Release --no-restore

echo "[build] test (Release)"
dotnet test "$SOLUTION_PATH" -c Release --no-build

echo "[build] publish ApiTester.Web -> $WEB_PUBLISH_DIR"
dotnet publish ApiTester.Web/ApiTester.Web.csproj -c Release --no-build -o "$WEB_PUBLISH_DIR"

echo "[build] publish ApiTester.Site -> $SITE_PUBLISH_DIR"
dotnet publish ApiTester.Site/ApiTester.Site.csproj -c Release --no-build -o "$SITE_PUBLISH_DIR"

echo "[build] complete. Artifacts at $ARTIFACT_ROOT"
