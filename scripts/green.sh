#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS_DIR="$ROOT_DIR/artifacts/test-results"
LOG_DIR="$RESULTS_DIR/logs"
mkdir -p "$RESULTS_DIR" "$LOG_DIR"

log() { printf '[green] %s\n' "$*"; }

run_step() {
  local name="$1"; shift
  log "$name"
  if "$@" 2>&1 | tee "$LOG_DIR/${name// /_}.log"; then
    return 0
  fi

  local exit_code=${PIPESTATUS[0]}
  log "FAILED: $name (exit code $exit_code)"
  log "Artifacts: $RESULTS_DIR"

  if compgen -G "$RESULTS_DIR/*.trx" > /dev/null; then
    log "Failing tests summary:"
    python3 - "$RESULTS_DIR" <<'PY'
import glob, os, sys, xml.etree.ElementTree as ET
results_dir = sys.argv[1]
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
found = False
for path in sorted(glob.glob(os.path.join(results_dir, '*.trx'))):
    try:
        root = ET.parse(path).getroot()
    except Exception:
        continue
    failed = root.findall('.//t:UnitTestResult[@outcome="Failed"]', ns)
    if not failed:
        continue
    found = True
    print(f"- {os.path.basename(path)}")
    for item in failed[:10]:
        print(f"  * {item.attrib.get('testName', '<unknown>')}")
if not found:
    print('- No failed tests found in TRX files.')
PY
  fi

  exit "$exit_code"
}

log "OS: $(uname -a)"
log "dotnet info:"
dotnet --info

run_step "dotnet restore" dotnet restore
run_step "dotnet build release" dotnet build -c Release
run_step "dotnet test release" dotnet test -c Release --logger "trx;LogFilePrefix=test_results" --results-directory "$RESULTS_DIR"

assemblies=$(find "$RESULTS_DIR" -maxdepth 1 -name '*.trx' | wc -l | tr -d ' ')
summary=$(python3 - "$RESULTS_DIR" <<'PY'
import glob, os, sys, xml.etree.ElementTree as ET
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
passed = failed = skipped = 0
for path in glob.glob(os.path.join(sys.argv[1], '*.trx')):
    root = ET.parse(path).getroot()
    counters = root.find('.//t:Counters', ns)
    if counters is None:
        continue
    passed += int(counters.attrib.get('passed', '0'))
    failed += int(counters.attrib.get('failed', '0'))
    skipped += int(counters.attrib.get('notExecuted', '0'))
print(f"passed={passed} failed={failed} skipped={skipped} total={passed+failed+skipped}")
PY
)

log "build success"
log "test assemblies executed: $assemblies"
log "test summary: $summary"
log "artifacts: $RESULTS_DIR"
