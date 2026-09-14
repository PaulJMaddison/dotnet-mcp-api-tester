# ICS.AI interview walkthrough

This guide is deliberately narrow. The goal is to explain one coherent engineering path in 10–15 minutes rather than tour the whole repository.

For the full test inventory and final verification commands, see `docs/interview/ics-ai-test-matrix.md`.

## One-sentence product story

Give the system an OpenAPI contract; it turns that contract into a controlled API-testing surface and can answer developer questions using project-scoped retrieved evidence instead of allowing the model to invent API behaviour.

## Recommended live flow

1. Start with the business problem: API documentation drifts, test collections rot, and a general LLM can confidently invent endpoints or parameters.
2. Run the deterministic demo once with the built-in fixture.
3. If Azure credentials/deployments are configured, show that the identical code path uses Azure chat + Azure embeddings.
4. Optionally run the same demo with an external OpenAPI JSON source using `--spec`.
5. Walk through the code below in order.
6. Finish with the tests and the safety/production trade-offs.

## Commands

Built-in fixture:

```powershell
dotnet run --project ApiTester.DemoWorkshop
```

External OpenAPI source:

```powershell
dotnet run --project ApiTester.DemoWorkshop -- `
  --spec "C:\path\to\openapi.json" `
  --question "How do I create a customer, what auth is explicitly documented, and what edge cases should I test?"
```

Azure-backed run:

```powershell
$env:AZURE_OPENAI_ENDPOINT="https://YOUR-RESOURCE.openai.azure.com"
$env:AZURE_OPENAI_CHAT_DEPLOYMENT="YOUR-CHAT-DEPLOYMENT"
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT="YOUR-EMBEDDING-DEPLOYMENT"
$env:AZURE_OPENAI_AUTHENTICATION="DefaultAzureCredential"

dotnet run --project ApiTester.DemoWorkshop
```

Run `az login` locally first. `DefaultAzureCredential` uses the authenticated Azure CLI session for local development and managed/workload identity when hosted in Azure. It obtains short-lived tokens in memory for `https://ai.azure.com/.default`; tokens are never persisted by the application.

API-key authentication remains an explicit fallback for isolated local testing:

```powershell
$env:AZURE_OPENAI_AUTHENTICATION="ApiKey"
$env:AZURE_OPENAI_API_KEY="YOUR-KEY"
```

Never commit the key or put it in a tracked settings file.

## The 7 files to show

### 1. `ApiTester.McpServer/Tools/RagTools.cs`

Why: this is the application boundary. It shows project-scoped indexing and asking, returns retrieval evidence separately from the generated answer, and keeps the model away from arbitrary execution.

Talking point: "The model answer is not my only observable. I return the retrieved chunk IDs, source and similarity score so retrieval can be debugged independently. Project and tenant context are enforced again when records are consumed, not trusted solely to the persistence query."

Likely challenge: the current vector store is in-memory. Answer: correct; this is a demo/local implementation behind `IVectorStore`. Production scale would use a durable/vector service without changing the answer service contract.

### 2. `ApiTester.Rag/Answering/RagAnswerService.cs`

Why: small orchestration class that makes the RAG pipeline obvious: embed question -> retrieve project-scoped evidence -> fail closed when no evidence -> build grounded prompt -> call model.

Talking point: "No evidence means no model call. I would rather return an explicit grounded failure than ask the LLM to fill the gap from its pretraining."

### 3. `ApiTester.Rag/Prompting/RagPromptBuilder.cs`

Why: demonstrates grounding and prompt-injection awareness.

Talking point: "An OpenAPI description is external/untrusted content. The prompt explicitly treats evidence as data even if somebody has put instruction-like text into an API description. It also explicitly forbids inventing parameters, request fields, auth and error behaviour."

Trade-off: prompt-level controls are defence-in-depth, not a security boundary by themselves. Network/tool permissions remain deterministic application policy.

### 4. `ApiTester.AI/Azure/AzureOpenAiTransport.cs`

Why: shows production engineering around the model API rather than a one-line SDK demo.

Talking points:
- HTTPS-only Azure endpoint.
- `DefaultAzureCredential`/Entra ID preferred, with explicit API-key or short-lived bearer-token test modes, without logging credentials.
- explicit timeout/cancellation.
- retry only for transient failure classes.
- `Retry-After` support.
- response-size bound.
- circuit breaker.
- errors expose status/request ID, not upstream bodies that might contain sensitive content.
- observability records safe operational metadata, never prompts/spec bodies or credentials.

Likely challenge: bearer token is static. Answer: it exists only for short-lived test verification. Normal local and production use `DefaultAzureCredential`, allowing Azure Identity to refresh and cache tokens in memory.

### 5. `ApiTester.AI/Azure/AzureOpenAiClient.cs`

Why: thin provider adapter over the transport. It sends deployment name + system/user messages and translates Azure usage/content into the provider-neutral `AiResponse` contract.

Talking point: "The transport owns reliability/security; the client owns the chat protocol. That keeps provider behaviour testable without spreading HTTP concerns through RAG."

### 6. `ApiTester.McpServer/Rag/AzureOpenAiEmbeddingClient.cs`

Why: proves retrieval can use a real Azure embedding deployment rather than a workshop fake.

Talking point: "Chat and embeddings are independently configurable. If I have chat but no embedding deployment the system remains usable and clearly reports that it has fallen back to deterministic lexical feature hashing."

### 7. Tests: use the test matrix rather than showing one happy-path test

Start with `docs/interview/ics-ai-test-matrix.md`, then open two or three representative tests rather than scrolling through the entire suite.

Good examples:
- `ApiTester.Rag.Tests/OpenApiContextContractTests.cs` — proves path/query/header/body variables, required/optional flags, auth, errors and source identity survive into grounded context without cross-project leakage.
- `ApiTester.Web.UnitTests/RagToolsContextTests.cs` — proves invalid IDs, missing context, project switching, project/tenant fail-closed checks and two-project isolation at the MCP boundary.
- `ApiTester.Web.UnitTests/AzureOpenAiEdgeCaseTests.cs` — proves the external model dependency is bounded around auth, retries, cancellation, malformed responses, vector failures, response sizes and circuit breaking.

Useful line to say:

"I test context as data with identity and boundaries, not just the final generated string. That means project IDs, spec IDs, parameter names, required flags, auth information and retrieved evidence are independently verifiable before the model answer is trusted."

## Architecture to draw verbally

```text
OpenAPI -> chunks -> embeddings -> project-scoped vector retrieval
                                      |
question -> embedding ----------------+
                                      |
                                      v
                           evidence + chunk IDs
                                      |
                                      v
                         grounded prompt boundary
                                      |
                                      v
                        Azure chat deployment
                                      |
                                      v
                         answer + visible evidence
```

MCP/API execution is a separate controlled boundary. The LLM does not receive arbitrary network access.

## Important trade-offs to acknowledge before they find them

- `InMemoryVectorStore` is suitable for a local demo, not multi-instance production. It now owns/clones vector state and supports re-embedding when the embedding model changes, but persistence and distributed indexing would be a production concern.
- The offline `DeterministicHashEmbeddingClient` is lexical feature hashing, not semantic embeddings; Azure embeddings are the real semantic path.
- `TextChunker` is generic. It now preserves small documents and short final tails, but a production OpenAPI specialist could improve retrieval further by creating operation/schema-aware chunks.
- A static bearer token is only a test verification option; `DefaultAzureCredential` is the preferred local and production path.
- The `AiCostEstimate` for an unknown cloud deployment is deliberately marked unknown rather than fabricating Azure pricing.
- Prompt-injection instructions are defence-in-depth. Deterministic execution policy is the actual authority boundary.

## Questions worth inviting

- Why keep `IAiClient` and `IEmbeddingClient` separate?
- Why fail closed when no evidence exists?
- Why expose retrieved evidence to the caller?
- Why enforce project/tenant context both in persistence and again at the consumption boundary?
- What happens when the embedding model changes but source content does not?
- Why not let the agent call arbitrary URLs itself?
- How would the in-memory vector store change at production scale?
- Where would managed identity fit?
- How would you evaluate retrieval quality and hallucination rate?

## Suggested closing line

"The model is intentionally the least trusted part of the design. OpenAPI is the source evidence, retrieval is visible, context identity is enforced, model calls are bounded, and execution remains application policy. That means I can change models or hosting providers without giving up the engineering controls around them."
