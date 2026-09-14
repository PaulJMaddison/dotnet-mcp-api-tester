# ICS.AI demo test matrix

This branch deliberately tests the AI path as an engineering system rather than treating a successful model call as sufficient evidence.

## 1. OpenAPI chunking and context retention

Files:
- `ApiTester.Rag.Tests/TextChunkerEdgeCaseTests.cs`
- `ApiTester.Rag.Tests/TextChunkerTailTests.cs`

Covered boundaries:
- null/invalid chunker options;
- invalid project/source identifiers;
- null/empty/whitespace input;
- OpenAPI documents smaller than the preferred minimum chunk size;
- exact minimum-size boundary;
- CRLF/blank-line normalisation;
- metadata, source ID, project ID and created-time preservation;
- deterministic chunk IDs and content hashes;
- hash changes when content changes;
- maximum chunk size;
- sequential IDs;
- preservation of a short final tail so end-of-document components/security context is not silently lost.

## 2. Embeddings and vector retrieval

File: `ApiTester.Rag.Tests/VectorStoreAndEmbeddingEdgeCaseTests.cs`

Covered boundaries:
- invalid embedding dimensions;
- empty text;
- deterministic output;
- unit-vector normalisation;
- shared API-term similarity;
- cancellation;
- null/empty upsert collections and invalid vector-store items;
- same-hash + same-vector idempotency;
- same source content can be re-embedded when the embedding model/vector changes;
- changed-content/hash replacement;
- vector state is cloned on upsert so caller mutation cannot alter indexed state;
- project-scoped chunk identity;
- empty project ID rejection;
- null/empty query vector rejection;
- top-K limit and ordering;
- invalid top-K;
- source type/source ID filters;
- case-insensitive metadata keys and values;
- unknown filters;
- embedding dimension mismatch;
- zero-vector similarity;
- cancelled store operations.

## 3. RAG orchestration

Files:
- `ApiTester.Rag.Tests/RagGroundingTests.cs`
- `ApiTester.Rag.Tests/RagPipelineContextTests.cs`

Covered boundaries:
- null dependencies;
- empty project ID;
- blank question;
- question trimming;
- top-K clamped to 1..20;
- project ID propagated unchanged to retrieval;
- exact evidence returned to caller;
- no-evidence means no model call;
- cancellation before dependency calls;
- null/empty index collections;
- null chunks;
- embedding order;
- one atomic store upsert per indexing pass;
- evidence source context preserved in the prompt;
- prompt-injection-like text remains inside an explicit untrusted-data boundary;
- anti-hallucination rules explicitly cover endpoints, parameters, request bodies, responses, authentication and error codes.

## 4. OpenAPI parameters and variables

File: `ApiTester.Rag.Tests/OpenApiContextContractTests.cs`

Covered context:
- path parameter names and required flags;
- query parameter names and optional flags;
- header parameters;
- request-body properties and required fields;
- parameter case preservation;
- bearer security schemes and operation security requirements;
- success and error response codes;
- absence of undocumented query parameters;
- malicious instruction-like descriptions treated only as evidence;
- identical concepts such as `id` kept isolated between different projects;
- evidence citation retains original spec/source identity.

## 5. MCP/project/tenant context boundary

Files:
- `ApiTester.Web.UnitTests/RagToolsContextTests.cs`
- `ApiTester.Web.UnitTests/ProjectToolsEdgeCaseTests.cs`

Covered boundaries:
- invalid GUID and all-zero GUID;
- no current project;
- explicit project switching;
- small-spec indexing;
- project with no specs;
- project context persisted through an ask;
- top-K extremes entering through MCP;
- blank questions;
- cancellation;
- store accidentally returning another project's spec fails closed;
- store accidentally returning another tenant's spec fails closed;
- two indexed projects cannot leak evidence between one another;
- project names are trimmed at creation;
- project listing lower-bound clamp;
- `ProjectContext` rejects `Guid.Empty`.

## 6. Azure OpenAI / Azure AI transport and protocol

Files:
- `ApiTester.Web.UnitTests/AzureOpenAiClientTests.cs`
- `ApiTester.Web.UnitTests/AzureOpenAiEdgeCaseTests.cs`
- `ApiTester.Web.UnitTests/AzureOpenAiIdentityTests.cs`

Covered boundaries:
- Azure OpenAI and Azure Foundry v1 endpoint construction;
- existing `/openai/v1` not duplicated;
- relative/HTTP/query/fragment endpoint rejection;
- API-key and bearer credentials;
- `DefaultAzureCredential` bearer acquisition with the Azure AI token scope;
- deterministic explicit authentication precedence and API-key compatibility;
- cancellation during identity token acquisition;
- shared Entra-authenticated transport for chat and embeddings;
- credential values excluded from payloads and exception diagnostics;
- chat and embeddings independently configured;
- invalid timeout/retry/input/response/circuit settings;
- blank path/null payload;
- pre-cancelled calls;
- bearer precedence over API key;
- missing credentials;
- non-transient HTTP failure is not retried;
- upstream error bodies are not copied into exceptions;
- request ID preserved for diagnostics;
- response-size limit;
- circuit opening and reset after success;
- chat context truncation;
- optional completion-token parameter;
- string/array response content;
- alternate usage field names;
- missing usage;
- missing/empty/malformed choices;
- invalid JSON;
- blank embedding input;
- embedding input truncation;
- missing/empty/non-numeric embedding vectors;
- 429 retry behaviour.

## 7. Legacy Web OpenAI provider

File: `ApiTester.Web.UnitTests/OpenAiProviderEdgeCaseTests.cs`

Covered boundaries:
- base URL validation;
- model and numeric configuration bounds;
- null dependencies;
- missing API key;
- caller cancellation;
- default vs pro model selection;
- bearer authorization;
- per-request URI/auth rather than mutating shared `HttpClient` defaults;
- operation/run identifiers and context explicitly labelled untrusted data;
- context and identifier length bounds;
- output truncation;
- derived output-token bounds;
- non-transient failure not retried;
- upstream error body not leaked;
- 429 retry;
- response-size bound;
- malformed successful response;
- invalid JSON;
- circuit opening and reset.

## Final mechanical gate

Run on the exact interview branch:

```powershell
git switch interview/ics-ai-demo-prep
git pull

dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Then run the repository's own local release gate:

```powershell
pwsh ./scripts/build.ps1
```

Finally, with Azure variables configured locally and never committed:

```powershell
dotnet run --project ApiTester.DemoWorkshop
```

The expected live path is:

`OpenAPI -> chunking -> Azure embeddings -> project-scoped retrieval -> grounded prompt -> Azure chat -> answer + visible evidence`.

Do not call the branch verified until the local build/test and live-provider run have both completed successfully.
