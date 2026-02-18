# Threat model (Day 117)

This threat model is scoped to the API execution platform in hosted and self-managed modes, with emphasis on outbound execution and evidence handling.

## Assets

- API tokens (API keys, bearer tokens, provider credentials)
- Tenant data (projects, OpenAPI specs, plans, runs, annotations)
- Run artifacts (reports, run exports, evidence bundles)
- Evidence packs and audit trails used for compliance decisions

## Trust boundaries

1. Browser/client ↔ ApiTester.Site/Ui
2. Site/Ui ↔ ApiTester.Web API
3. ApiTester.Web ↔ outbound targets under test (egress boundary)
4. ApiTester.Web ↔ database/persistence layer
5. API output ↔ generated artifacts and evidence packs

## Practical threats (STRIDE-oriented)

- **SSRF and egress abuse**: crafted specs or run definitions target localhost, private ranges, metadata endpoints, or internal services.
- **DNS rebinding / redirect abuse**: allowed host redirects to blocked/private targets, or resolves to unsafe IPs after policy checks.
- **Credential leakage**: Authorization headers, tokens, and secrets appear in logs, reports, run payloads, or evidence exports.
- **Abuse / quota bypass**: repeated run/export/import activity overwhelms service limits or bypasses plan gates.
- **OpenAPI import abuse**: oversized/padded specs, decompression/zip-bomb style payloads, or pathological documents consume memory/CPU.
- **Tenant isolation failures**: cross-tenant reads/writes of projects, runs, exports, or audit metadata.

## Mitigations implemented

- Hosted mode deny-by-default when allowlists are missing.
- SSRF policy guardrails: explicit allowlists, localhost/private/link-local restrictions, DNS/IP validation, and redirect policy checks.
- Redirect hardening in test execution with loop detection and redirect hop limits.
- OpenAPI import size caps and remote fetch size enforcement.
- Redaction service applied to run artifacts, reports, and evidence bundles.
- API key/bearer redaction middleware before auth handling.
- Audit logging for run and export flows.
- Subscription and abuse controls for exports and high-cost operations.

## Security regression checklist

Run the local security regression suite before release:

1. SSRF regression tests (blocked loopback, private ranges, redirects, normalization tricks)
2. Token leakage regressions (exports/reports/evidence redaction, sanitized errors)
3. Abuse regressions (evidence export gating, import size limits, limiter behavior)
4. Build/test in Release mode

Commands:

- `./scripts/security.sh`
- `pwsh ./scripts/security.ps1`

The suite is deterministic and uses local fixtures only (no real external endpoints).
