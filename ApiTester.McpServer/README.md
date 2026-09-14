# dotnet-mcp-api-tester MCP server

A .NET 8 Model Context Protocol server that turns OpenAPI contracts into a controlled surface for API discovery, testing and grounded AI assistance.

The execution boundary stays deterministic: API calls go through explicit policy, SSRF protection, method restrictions, timeouts and size limits. AI is used for explanation and grounded reasoning; it does not bypass the execution policy.

## Grounded RAG flow

```text
OpenAPI specification
        |
        v
TextChunker
        |
        v
IEmbeddingClient
  |               |
  | local/offline | Azure OpenAI embedding deployment
  v               v
InMemoryVectorStore
        |
        v
RagAnswerService
        |
        +--> retrieved evidence + chunk IDs
        |
        v
RagPromptBuilder
        |
        v
IAiClient
  |               |
  | local/offline | Azure OpenAI chat deployment
  v               v
grounded answer + evidence
```

The prompt boundary treats OpenAPI text as **untrusted evidence data**. Instructions embedded in API descriptions must not override the grounding rules. When no indexed evidence exists, the model is not called.

The built-in local embedding implementation is deterministic lexical feature hashing for tests/offline demonstrations. It is deliberately not presented as a semantic embedding model. Configure an Azure embedding deployment for real semantic retrieval.

## Azure OpenAI / Microsoft Foundry v1

The Azure path uses the OpenAI-compatible v1 routes:

- `POST {endpoint}/openai/v1/chat/completions`
- `POST {endpoint}/openai/v1/embeddings`

`Endpoint` may be the resource endpoint (for example `https://name.openai.azure.com`) or an endpoint already ending in `/openai/v1`. The client normalises the base path.

Preferred local demo configuration is through environment variables so credentials never enter source control:

```powershell
$env:AZURE_OPENAI_ENDPOINT="https://YOUR-RESOURCE.openai.azure.com"
$env:AZURE_OPENAI_CHAT_DEPLOYMENT="YOUR-CHAT-DEPLOYMENT"
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT="YOUR-EMBEDDING-DEPLOYMENT"
$env:AZURE_OPENAI_API_KEY="YOUR-KEY"
```

A short-lived bearer token can be supplied instead of an API key:

```powershell
$env:AZURE_OPENAI_AUTH_TOKEN="YOUR-BEARER-TOKEN"
```

For a long-running production deployment, prefer workload identity / managed identity with automatic token refresh rather than persisting a bearer token. This repository deliberately keeps the local demo credential boundary explicit rather than pretending a static token is production identity management.

Optional configuration keys:

```text
AzureOpenAI:TimeoutSeconds
AzureOpenAI:MaxRetries
AzureOpenAI:MaxResponseBytes
AzureOpenAI:MaxInputChars
AzureOpenAI:MaxCompletionTokens
AzureOpenAI:CircuitBreakerFailureThreshold
AzureOpenAI:CircuitBreakerBreakSeconds
```

`MaxCompletionTokens` is unset by default because Foundry v1 can front model families with different supported generation controls. Set it only after choosing the deployed model.

If Azure chat is not fully configured, the server uses `LocalGroundedAiClient`. If Azure embeddings are not fully configured, it uses deterministic lexical feature hashing. Partial Azure settings therefore do not make the server unusable.

## Reliability boundary

The Azure transport provides:

- HTTPS-only endpoint validation;
- API-key or bearer authentication without logging credentials;
- explicit cancellation and per-attempt timeouts;
- bounded retries for timeout, `408`, `429` and `5xx` responses;
- `Retry-After` support;
- bounded response size;
- a simple consecutive-failure circuit breaker;
- safe error messages containing status/request ID but not upstream response bodies.

## Run the MCP server

From the repository root:

```powershell
dotnet run --project ApiTester.McpServer
```

The server communicates over stdio. Logs are intentionally written to stderr so stdout remains valid MCP JSON-RPC traffic.

## Interview/demo runner

The deterministic built-in demo creates a project, imports a known OpenAPI fixture, indexes it and asks a grounded question:

```powershell
dotnet run --project ApiTester.DemoWorkshop
```

With Azure environment variables configured, the same command automatically uses Azure chat and Azure embeddings.

You can also point the demo at another OpenAPI JSON source that the application is legitimately allowed to read:

```powershell
dotnet run --project ApiTester.DemoWorkshop -- \
  --spec "C:\path\to\openapi.json" \
  --question "How do I create a customer, what authentication is documented, and what edge cases should I test?"
```

`--spec` is passed through the application's existing OpenAPI import boundary. Do not use the tool to probe APIs you do not own or have permission to test.

## Security stance

Runtime API execution is separate from model reasoning. Existing controls include host allowlisting, SSRF protection, safe methods by default, output redaction, request/response limits and timeouts. The model receives evidence for reasoning; it does not choose arbitrary network destinations or credentials.

## Main RAG tools

- `api_rag_index_project` — chunk and index a project's OpenAPI specifications.
- `api_rag_ask` — retrieve project-scoped evidence and answer using that evidence.

Evidence returned from `api_rag_ask` includes chunk ID, source, score and a preview so retrieval can be inspected independently of the model answer.
