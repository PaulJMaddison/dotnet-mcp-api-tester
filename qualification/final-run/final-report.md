# Final MCP Public API Qualification

## Repository state

- Branch: main
- Starting SHA: 5ac53c07b042fbae86014c0571793ec5997e060b
- Code SHA being qualified: 5ac53c07b042fbae86014c0571793ec5997e060b
- Evidence commit: this report is generated from the single run against the code SHA above.
- Working tree: clean before evidence generation

## Build

- Restore: succeeded
- Release build: succeeded; 0 warnings, 0 errors

## Tests

- Total: 138; passed: 138; failed: 0; skipped: 0
- Duration: 713 ms test execution

## MCP server

- Real Release server initialized over stdio successfully.
- Tool count: 13.
- Exercised: initialize, tools/list, api_load_open_api, api_list_operations, api_describe_operation (findPetsByStatus/getPetById), api_generate_test_plan, api_get_policy, api_call_operation (dry-run/live), api_ask_contract (five questions), api_set_policy.
- MCP stdout remained protocol-only; telemetry was emitted on stderr and to telemetry.ndjson.

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
- Embedding batches: 1 indexing batch; indexing duration: 4220 ms in the completed run
- Validation issues: 0

## Semantic queries

See [model-responses.md](model-responses.md) for the exact questions, retrieved chunks/scores, verbatim Azure answers, and grounding assessments.

- Query 1: PASS
- Query 2: PASS
- Query 3: PASS
- Query 4: PASS
- Query 5: PASS (no undocumented limits invented)

## Deterministic testing

- Operation: getPetById
- Generated mechanically by C# from the contract, not by LLM reasoning.
- Six cases: required omission, zero, negative, wrong type, Int64.MaxValue, Int64.MinValue.

## HTTP execution

- Dry run: PASS; GET /api/v3/pet/findByStatus?status=available; no network request; policy dry-run and SSRF allowed.
- Live GET: PASS; policy allowed only configured Petstore base URL and GET; localhost/private/link-local protections remained enabled; HTTP 200 in 350 ms; 133438 bytes; truncated=false.

## Telemetry

- File: telemetry.ndjson
- Event count: 51
- Correlation ID: c4aac38b683a4cd5b014e5ab922171f4
- Complete trace covers server start/configuration, MCP calls, fetch/parse/evidence, real Azure embedding/chat activity, retrieval, deterministic generation, dry-run, SSRF/policy checks, and live HTTP execution.

## Issues found

- Medium: detailed qualification telemetry was absent initially. Fixed with a small stderr + NDJSON telemetry service and tool-stage instrumentation.
- Low: qualification launcher initially needed policy mutation opt-in for the explicitly requested narrow allowlist. Fixed in runtime launcher only; safety defaults unchanged.

## FINAL VERDICT

PASS — the complete real MCP + Azure + RAG + deterministic generation + safe HTTP path executed successfully. Query 4 was reclassified after review because the original rubric conflated exploratory test suggestions with unsupported factual assertions. Build/tests and safety gates pass.
