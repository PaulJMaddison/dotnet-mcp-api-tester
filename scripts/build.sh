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
DOCKER_ENABLED=false
SMOKE_ENABLED=false

for arg in "$@"; do
  case "$arg" in
    --docker)
      DOCKER_ENABLED=true
      ;;
    --smoke)
      SMOKE_ENABLED=true
      DOCKER_ENABLED=true
      ;;
    *)
      echo "Unknown argument: $arg" >&2
      echo "Usage: scripts/build.sh [--docker] [--smoke]" >&2
      exit 1
      ;;
  esac
done

cd "$ROOT_DIR"

dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --logger trx

if [ "$DOCKER_ENABLED" = true ]; then
  docker build -f ApiTester.Web/Dockerfile -t apitester-web:local .
  docker build -f ApiTester.Site/Dockerfile -t apitester-site:local .
fi

if [ "$SMOKE_ENABLED" = true ]; then
  "$ROOT_DIR/scripts/smoke.sh"
fi
