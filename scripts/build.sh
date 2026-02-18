#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
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
