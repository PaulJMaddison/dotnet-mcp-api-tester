# Final MCP Public API Qualification

## Repository state

- Branch: main
- Starting SHA: 6be5f24f6b5c853171c62494e8ec100e77e5da4c
- Qualification evidence SHA: 7fceca81ce27179bdf29b5a00d0a9005fe31eff1
- Working tree after qualification push: clean

## Build

- Restore: succeeded
- Release build: succeeded; 0 warnings, 0 errors

## Tests

- Total: 135; passed: 135; failed: 0; skipped: 0
- Duration: 569 ms test execution

## MCP server

- Real Release server initialized over stdio successfully.
- Tool count: 13.
- Exercised: initialize, tools/list, api_load_open_api, api_list_operations, api_describe_operation (findPetsByStatus/getPetById), api_generate_test_plan, api_get_policy, api_call_operation (dry-run/live), api_ask_contract (five questions), api_set_policy.
- MCP stdout remained protocol-only; qualification telemetry was emitted on stderr and to telemetry.ndjson.

## Azure AI

- Authentication: DefaultAzureCredential
- Credential source: AzureCli
- Chat deployment: gpt-41-mini-demo
- Embedding deployment: text-embedding-3-small-demo
- Real embedding calls confirmed: yes; 1 indexing batch of 27 plus 5 query batches, HTTP 200.
- Real chat/reasoning calls confirmed: yes; 5 HTTP 200 calls.
- Retries: 0 observed. Token usage was returned for reasoning calls.
- No credentials or tokens were recorded.

## OpenAPI load

- URL: https://petstore3.swagger.io/api/v3/openapi.json
- Title/version: Swagger Petstore - OpenAPI 3.0 / 1.0.27
- Paths: 13; operations: 19; schemas: 6; security schemes/evidence: 2; evidence/vector count: 27
- Embedding batches: 1 indexing batch; indexing duration: 2441 ms in the completed run
- Validation issues: 0

## Semantic queries

See [model-responses.md](model-responses.md) for the exact questions, retrieved chunks/scores and verbatim Azure answers.

- Query 1: PASS
- Query 2: PASS
- Query 3: PARTIAL after post-run review. The model faithfully repeated an incorrect `IN: Query` field that the evidence builder emitted for the OAuth2 scheme. OpenAPI `in` applies to API-key schemes, not OAuth2. The evidence-generation defect was identified after the qualification run and is fixed in the subsequent hardening change; this query should be rerun before release evidence is considered final.
- Query 4: PARTIAL. The answer included useful inferred test suggestions beyond constraints explicitly stated by the contract.
- Query 5: PASS (no undocumented limits invented)

## Deterministic testing

- Operation: getPetById
- Generated mechanically by C# from the contract, not by LLM reasoning.
- Six cases: required omission, zero, negative, wrong type, Int64.MaxValue, Int64.MinValue.

## HTTP execution

- Dry run: PASS; GET /api/v3/pet/findByStatus?status=available; no network request; policy dry-run and SSRF allowed.
- Live GET: PASS; policy allowed only configured Petstore base URL and GET; localhost/private/link-local protections remained enabled; HTTP 200 in 350 ms; 133301 bytes; truncated=false.

## Telemetry

- File: telemetry.ndjson
- Event count: 100
- Correlation ID: 93ddfdc9f78d46ccba1e649e1148d557
- Complete trace covers server start/configuration, MCP calls, fetch/parse/evidence, real Azure embedding/chat activity, retrieval, deterministic generation, dry-run, SSRF/policy checks, and live HTTP execution.
- Post-run hardening makes durable qualification telemetry opt-in via `API_TESTER_QUALIFICATION_DIR` and strips URL query values / secret-shaped fields from emitted telemetry. Normal MCP usage therefore remains non-persistent.

## Issues found

- Medium: detailed qualification telemetry was absent initially. Fixed with a small stderr + NDJSON telemetry service and tool-stage instrumentation.
- Medium: OAuth2 security evidence incorrectly emitted `IN: Query` because the OpenAPI model's `In` value was rendered for every security scheme. Fixed so location is emitted only for API-key schemes.
- Medium: qualification telemetry originally wrote a durable file by default. Fixed so file/stderr qualification telemetry is disabled unless `API_TESTER_QUALIFICATION_DIR` is explicitly configured.
- Medium: telemetry originally recorded complete request URLs, including query values. Fixed by sanitizing URL/source fields before emission and redacting secret-shaped fields.
- Low: qualification launcher initially needed policy mutation opt-in for the explicitly requested narrow allowlist. Fixed in runtime launcher only; safety defaults unchanged.
- Low: qualification launcher contained account-specific Azure resource/deployment defaults. Fixed so runtime Azure settings must be supplied by the operator environment and are not committed into the reusable script.

## FINAL VERDICT

PARTIAL — the complete real MCP + Azure + RAG + deterministic generation + safe HTTP path executed successfully, but post-run review found one semantic security-evidence defect and Query 4 contains inference beyond explicit constraints. The security-evidence defect and telemetry hygiene issues are fixed in the follow-up hardening change. A short requalification run should refresh Query 3 and the final evidence before public v1 release.
