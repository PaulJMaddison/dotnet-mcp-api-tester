$dir = Join-Path (Get-Location) 'qualification/final-run'
$messages = Get-Content (Join-Path $dir 'mcp-transcript.jsonl') | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.direction -eq 'response' }
function Result($id) { ($messages | Where-Object id -eq $id).message.result.content[0].text | ConvertFrom-Json }
$questions = @(
 'I need to retrieve pets based on their status. Which documented operation should I use, what HTTP method and path does it use, and what parameter values are documented?',
 'I have a pet ID and need to retrieve that individual pet. Which operation should I call, where is the ID supplied, and what responses are documented?',
 'What authentication or security requirements are documented for the operations used to retrieve pets?',
 'Based only on the OpenAPI contract, what edge cases should I test for the pet ID input to the operation that retrieves one pet?',
 'What rate limits, retry limits and requests-per-minute restrictions does this API impose? Give me the exact documented limits.'
)
$verdicts = @('PASS','PASS','PASS','PARTIAL','PASS')
$md = [Collections.Generic.List[string]]::new()
for($i=0;$i -lt 5;$i++) {
  $r = Result (10+$i)
  $md.Add("## Query $($i+1)`n`n### Question`n`n$($questions[$i])`n`n### Retrieved evidence`n")
  foreach($e in $r.evidence) { $md.Add("- $($e.chunkId) — $($e.evidenceType) — score $($e.score)" + $(if($e.operationId){" — operation $($e.operationId)"}else{''})) }
  $md.Add("`n### Model answer`n`n$($r.answer)`n`n### Grounding assessment`n`n$($verdicts[$i]) — " + $(switch($i){3{'The contract supports required path, integer int64 and documented 400/404 responses; some proposed cases such as empty input and negative/zero validity are test suggestions or inferences, not explicit contract constraints.'}4{'The response correctly states that no exact rate/retry/request-per-minute limits are documented and does not invent limits.'}default{'Material claims are supported by the retrieved OpenAPI evidence.'}}) + "`n")
}
$md -join "`n" | Set-Content (Join-Path $dir 'model-responses.md')
$load=Result 3; $plan=Result 7; $dry=Result 9; $live=Result 21
$eventCount=(Get-Content (Join-Path $dir 'telemetry.ndjson')).Count
$report=@"
# Final MCP Public API Qualification

## Repository state

- Branch: main
- Starting SHA: 6be5f24f6b5c853171c62494e8ec100e77e5da4c
- Ending SHA: pending commit
- Working tree: qualification changes pending review

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
- Embedding batches: 1 indexing batch; indexing duration: 2441 ms in the completed run
- Validation issues: 0

## Semantic queries

See [model-responses.md](model-responses.md) for the exact questions, retrieved chunks/scores, verbatim Azure answers, and grounding assessments.

- Query 1: PASS
- Query 2: PASS
- Query 3: PASS
- Query 4: PARTIAL
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
- Event count: $eventCount
- Correlation ID: 93ddfdc9f78d46ccba1e649e1148d557
- Complete trace covers server start/configuration, MCP calls, fetch/parse/evidence, real Azure embedding/chat activity, retrieval, deterministic generation, dry-run, SSRF/policy checks, and live HTTP execution.

## Issues found

- Medium: detailed qualification telemetry was absent initially. Fixed with a small stderr + NDJSON telemetry service and tool-stage instrumentation.
- Low: qualification launcher initially needed policy mutation opt-in for the explicitly requested narrow allowlist. Fixed in runtime launcher only; safety defaults unchanged.

## FINAL VERDICT

PARTIAL — the complete real MCP + Azure + RAG + deterministic generation + safe HTTP path executed successfully, but Query 4 is conservatively PARTIAL because the model included inferred test suggestions not explicitly constrained by the contract. Build/tests and safety gates pass.
"@
$report | Set-Content (Join-Path $dir 'final-report.md')
Write-Output "events=$eventCount"
Write-Output "model-responses.md and final-report.md written"
