# Changelog

## Day 115 — Commercial hardening and release readiness
- Added release build scripts with restore/build/test quality gates and production placeholder/stub scanning.
- Standardized API ProblemDetails shape with stable `errorCode`, `correlationId`, and `timestampUtc` extensions.
- Hardened API token auth and rate-limit failure responses to use stable machine-readable error codes.
- Finalized Site OIDC fallback behavior with a production-ready auth-not-configured app page.
- Added manual-only CI and release workflows for Release validation and artifact packaging.
- Added MIT LICENSE and updated centralized version metadata for commercial packaging.

## Day 114
- Enforced schema upgrade policy handling and startup migrations gating.

## Day 113
- Hardened hosted egress controls and SSRF checks.

## Day 112
- Improved quota enforcement and billing guardrails.

## Day 111
- Expanded evidence/report export reliability and deterministic output.

## Day 110
- Improved API token lifecycle management and auditability.

## Day 109
- Strengthened run retention and data lifecycle behavior.

## Day 108
- Added integration tests for tenant isolation and auth boundary checks.

## Day 107
- Improved OpenAPI ingestion safety limits and validation messaging.

## Day 106
- Improved run execution diagnostics and classification analytics.

## Day 105
- Added AI usage gating and guarded generation endpoints.

## Day 104
- Improved persistence provider abstraction and SQL readiness.

## Day 103
- Expanded UI/Site app coverage and onboarding flows.

## Day 102
- Baseline release pipeline hardening and startup diagnostics.
