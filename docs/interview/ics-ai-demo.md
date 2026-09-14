# ICS.AI interview walkthrough

This guide is deliberately narrow. The goal is to explain one coherent engineering path in 10–15 minutes rather than tour the whole repository.

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
$env:AZURE_OPENAI_API_KEY="YOUR-KEY"

dotnet run --project ApiTester.DemoWorkshop
```

Never commit the key.

## The 7 files to show

### 1. `ApiTester.McpServer/Tools/RagTools.cs`

Why: this is the application boundary. It shows project-scoped indexing and asking, returns retrieval evidence separately from the generated answer, and keeps the model away from arbitrary execution.

Talking point: "The model answer is not my only observable. I return the retrieved chunk IDs, source and similarity score so retrieval can be debugged independently."

Likely challenge: the current vector store is in-memory. Answer: correct; this is a demo/local implementation behind `IVectorStore`. Production scale would use a durable/vector service without changing the answer service contract.

### 2. `ApiTester.Rag/Answering/RagAnswerService.cs`

Why: small orchestration class that makes the RAG pipeline obvious: embed question -> retrieve project-scoped evidence -> fail closed when no evidence -> build grounded prompt -> call model.

Talking point: "No evidence means no model call. I would rather return an explicit grounded failure than ask the LLM to fill the gap from its pretraining."

### 3. `ApiTester.Rag/Prompting/RagPromptBuilder.cs`

Why: demonstrates grounding and prompt-injection awareness.

Talking point: "An OpenAPI description is external/untrusted content. The prompt explicitly treats evidence as data even if somebody has put instruction-like text into an API description."

Trade-off: prompt-level controls are defence-in-depth, not a security boundary by themselves. Network/tool permissions remain deterministic application policy.

### 4. `ApiTester.AI/Azure/AzureOpenAiTransport.cs`

Why: shows production engineering around the model API rather than a one-line SDK demo.

Talking points:
- HTTPS-only Azure endpoint.
- key or bearer auth without logging credentials.
- explicit timeout/cancellation.
- retry only for transient failure classes.
- `Retry-After` support.
- response-size bound.
- circuit breaker.
- errors expose status/request ID, not upstream bodies that might contain sensitive content.

Likely challenge: bearer token is static. Answer: it exists for short-lived local verification only. Production should use managed/workload identity with automatic refresh.

### 5. `ApiTester.AI/Azure/AzureOpenAiClient.cs`

Why: thin provider adapter over the transport. It sends deployment name + system/user messages and translates Azure usage/content into the provider-neutral `AiResponse` contract.

Talking point: "The transport owns reliability/security; the client owns the chat protocol. That keeps provider behaviour testable without spreading HTTP concerns through RAG."

### 6. `ApiTester.McpServer/Rag/AzureOpenAiEmbeddingClient.cs`

Why: proves retrieval can use a real Azure embedding deployment rather than a workshop fake.

Talking point: "Chat and embeddings are independently configurable. If I have chat but no embedding deployment the system remains usable and clearly reports that it has fallen back to deterministic lexical feature hashing."

### 7. Tests: `ApiTester.Rag.Tests/RagGroundingTests.cs` and `ApiTester.Web.UnitTests/AzureOpenAiClientTests.cs`

Why: finish with evidence.

Show:
- local retrieval ranks shared API terms ahead of unrelated terms;
- instruction-like content inside evidence remains inside the untrusted-data boundary;
- zero evidence avoids a model call;
- Azure v1 endpoint/deployment/API-key contract;
- embeddings response parsing;
- 429 retry behaviour.

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

- `InMemoryVectorStore` is suitable for a local demo, not multi-instance production.
- The offline `DeterministicHashEmbeddingClient` is lexical feature hashing, not semantic embeddings; Azure embeddings are the real semantic path.
- `TextChunker` is generic. A production OpenAPI specialist could improve retrieval further by creating operation/schema-aware chunks.
- A static bearer token is only a local verification option; production identity should refresh automatically.
- The `AiCostEstimate` for an unknown cloud deployment is deliberately marked unknown rather than fabricating Azure pricing.
- Prompt-injection instructions are defence-in-depth. Deterministic execution policy is the actual authority boundary.

## Questions worth inviting

- Why keep `IAiClient` and `IEmbeddingClient` separate?
- Why fail closed when no evidence exists?
- Why expose retrieved evidence to the caller?
- Why not let the agent call arbitrary URLs itself?
- How would the in-memory vector store change at production scale?
- Where would managed identity fit?
- How would you evaluate retrieval quality and hallucination rate?

## Suggested closing line

"The model is intentionally the least trusted part of the design. OpenAPI is the source evidence, retrieval is visible, model calls are bounded, and execution remains application policy. That means I can change models or hosting providers without giving up the engineering controls around them."
