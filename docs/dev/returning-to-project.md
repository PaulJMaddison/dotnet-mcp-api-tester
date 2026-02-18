# Returning to the project

This repo is parked in a known-good pause state. Use these steps to restore confidence quickly.

## 1) Prerequisites

- .NET SDK version from `global.json` (locked)
- Docker + Docker Compose
- PowerShell 7+ (for `.ps1` scripts)
- Bash (for `.sh` scripts)

## 2) Restore/build/test (single command)

Canonical full-gate commands:

- Linux/macOS:
  - `scripts/all-tests.sh`
- Windows/PowerShell:
  - `scripts/all-tests.ps1`

These commands run the full verification battery in order:

1. `dotnet restore`
2. `dotnet build -c Release`
3. `dotnet test -c Release`
4. Smoke/compose flow
5. E2E suite (Playwright)
6. Security regression suite
7. Perf sanity check (if perf harness scripts exist)

Artifacts are written to:

- `artifacts/all-tests/<timestamp>/`

## 3) Individual commands (if needed)

- Build only: `dotnet build -c Release`
- Default tests only: `dotnet test -c Release`
- Smoke only: `scripts/smoke.sh` or `scripts/smoke.ps1`
- E2E only: `scripts/e2e.sh` or `scripts/e2e.ps1`
- Security only: `scripts/security.sh` or `scripts/security.ps1`

## 4) Local environment template

Use `docs/ops/local-env-template.env` as your starting point for local `.env` values.
No production secrets should be committed.
