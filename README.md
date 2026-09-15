# .NET MCP API Tester

**Give your coding agent an API test engineer.**

`dotnet-mcp-api-tester` is a local .NET MCP server for Claude Code and Codex. Point it at an OpenAPI / Swagger contract and your coding agent can ask free-text API questions, find operations, inspect parameters and schemas, generate parameter tests and edge cases, safely execute requests, and analyse the results.

The MCP server itself makes the HTTP calls, so it can test localhost, internal, staging or public APIs that you are authorised to test. **Nothing is persisted.** There is no database, web UI, SaaS backend or stored test-run history. Restart the MCP process and the loaded API definition, vector index, target-API auth and execution-policy state disappear.

> **The OpenAPI contract decides what is true, deterministic code decides what is mechanically testable and safe, and the LLM handles the reasoning that remains.**

## v1 scope

The first release is deliberately small:

- local `stdio` MCP server
- .NET 8
- Azure OpenAI only
- OpenAPI / Swagger as the source of truth
- semantic operation / schema / security evidence
- batched Azure embeddings
- in-memory vector retrieval
- grounded Azure AI reasoning
- deterministic constraint-derived edge cases
- safe direct HTTP execution
- no persistence and no external vector database

OpenAI direct, Gemini, Claude reasoning providers and other model backends are intentionally outside the v1 scope.

The MCP server owns its Azure credential locally; Claude and Codex never receive it as model-visible data.

## How it works

```text
Claude / Codex
      │
      │ MCP stdio
      ▼
┌──────────────────────────────┐
│     API Tester MCP Server    │
│                              │
│ OpenAPI loader               │
│ OpenAPI semantic evidence    │
│ Azure embeddings             │
│ In-memory vector store       │
│ Contract retrieval / RAG     │
│ Deterministic edge cases     │
│ HTTP execution + safety      │
│ Azure AI reasoning           │
└──────────────────────────────┘
             │
             ▼
      localhost / dev /
      staging / public API
```

Loading a contract is atomic:

```text
api_load_open_api
      ↓
download / read
      ↓
parse OpenAPI
      ↓
normalise operation identities
      ↓
build operation / schema / security evidence
      ↓
batch Azure embeddings
      ↓
replace in-memory vector index
      ↓
READY
```

The RAG implementation deliberately does **not** split OpenAPI JSON every N characters. `OpenApiEvidenceBuilder` keeps the domain boundaries intact: an operation stays an operation, a component schema stays a schema and a security scheme stays a security scheme.

The vector store is intentionally in-process. OpenAPI is the source of truth; vectors are disposable derived state. That means no Pinecone, Elasticsearch, PostgreSQL/pgvector or other vector infrastructure is required.

## Requirements

- .NET 8 runtime (the SDK is needed only when building from source)
- Codex CLI or Claude Code
- Azure OpenAI chat deployment
- Azure OpenAI embedding deployment
- Azure credentials using Azure CLI / managed identity / API key / bearer token

Azure AI is required. v1 intentionally has no local, mock or deterministic AI fallback.

## Quick start

Install the .NET global tool, configure Azure OpenAI, then register the command with Codex or Claude Code:

```bash
dotnet tool install --global PaulJMaddison.DotnetMcpApiTester
```

```bash
claude mcp add --scope user api-tester -- mcp-api-tester
codex mcp add api-tester -- mcp-api-tester
```

Check registration with `claude mcp list` or `codex mcp list`. The first agent prompt can be:

> Load `https://petstore3.swagger.io/api/v3/openapi.json`, find the operation that retrieves a pet by ID, and generate edge cases for its `petId` parameter.

The packaged tool requires the .NET 8 runtime. To build from source instead:

```bash
dotnet restore DotnetMcpApiTester.sln
dotnet build DotnetMcpApiTester.sln -c Release
dotnet test DotnetMcpApiTester.sln -c Release
```

The solution deliberately contains only:

```text
ApiTester.McpServer
ApiTester.Rag
ApiTester.AI
ApiTester.McpServer.Tests
```

## Azure authentication

Azure authentication belongs to the **local MCP process**, not to the model. Claude or Codex never needs to receive your Azure credential and there is deliberately no MCP tool for setting an Azure API key or bearer token.

### Recommended: Azure CLI / Microsoft Entra ID

Authenticate locally:

```powershell
az login
```

The signed-in identity must have permission to use the Azure OpenAI resource. Then configure the resource and deployments:

```powershell
$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='AzureCli'
```

The credential boundary is:

```text
Claude / Codex
      ↓ MCP stdio
API Tester MCP process
      ↓ Azure.Identity / Azure CLI credential
Azure OpenAI
```

**The model never sees the Azure credential.**

For Azure-hosted execution, use managed identity instead:

```powershell
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='ManagedIdentity'
```

### Alternative: Azure OpenAI API key

```powershell
$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='ApiKey'
$env:AZURE_OPENAI_API_KEY='<your key>'
```

Keep the real key in the local process environment or an MCP-client secret/environment configuration. Never commit it and never paste it into a Claude/Codex prompt.

If configuration is missing or invalid, the MCP process exits before starting stdio and writes actionable setup guidance to **stderr**. It never echoes configured keys or tokens.

## Connect to Codex

After installing the global tool:

```bash
codex mcp add api-tester -- mcp-api-tester
```

Check the registration:

```bash
codex mcp list
```

Equivalent Codex configuration:

```toml
[mcp_servers.api-tester]
command = "dotnet"
args = ["run", "--project", "./ApiTester.McpServer/ApiTester.McpServer.csproj"]
```

If your MCP client does not inherit the shell environment, configure the Azure settings in that client's MCP process environment rather than passing credentials through chat.

## Connect to Claude Code

After installing the global tool:

```bash
claude mcp add --scope user api-tester -- mcp-api-tester
```

Check it:

```bash
claude mcp get api-tester
claude mcp list
```

Use `--scope user` if you want the MCP server available across Claude Code projects rather than only the current project.

## Core MCP workflow

```text
api_load_open_api
      ↓
api_list_operations / api_describe_operation
      ↓
api_search_contract / api_ask_contract
      ↓
api_generate_test_plan
      ↓
api_get_policy
      ↓
api_call_operation (dry-run first)
      ↓
live API call when explicitly allowed
```

Example:

> Load `./openapi.json`. Show me the available operations, find the endpoint for retrieving orders, generate the edge cases for that operation and dry-run the requests. Use only the OpenAPI contract as documented truth and do not make a live call until I approve it.

The deterministic test generator derives mechanically provable cases from the contract itself: required/optional/nullable parameters, numeric boundaries, string lengths and patterns, enums, formats, arrays, nested objects, composed schemas and related OpenAPI constraints. The LLM is not asked to invent those cases.

## Safety model

The model cannot create new network authority for itself.

Live execution is constrained by:

```text
allowed target URL(s)
        +
allowed HTTP method(s)
        +
network safety rules
        +
request / response limits
```

Execution starts in **dry-run** mode. Live calls are deny-by-default until an allowed base URL is configured. Localhost and private networks are blocked by default.

Policy mutation through MCP is disabled unless a human explicitly enables it before process startup:

```powershell
$env:APITESTER_MCP_ALLOW_POLICY_MUTATION='true'
```

Use that only for a supervised development session. Then allow only the target URL and HTTP methods you actually want the agent to exercise. Link-local/cloud metadata addresses remain blocked.

Target-API bearer tokens and base-URL overrides live only in the process. Azure AI credentials are process-start configuration and are never exposed through MCP tools. Nothing is stored anywhere.

## Why in memory?

An API definition normally produces hundreds or a few thousand evidence chunks, not millions. A linear in-memory cosine-similarity scan is therefore simple, fast and cheap for this use case.

More importantly, the vector index is not authoritative state. It can always be recreated from the OpenAPI contract. Persisting it would add hosting cost, credentials, network I/O, patching, backup and additional security surface without providing much value to a local developer MCP tool.

## Testing philosophy

The repository keeps tests around the actual MCP product rather than deleted application surfaces. Coverage includes OpenAPI identity and semantics, parameter and request-body edge cases, semantic evidence generation, vector retrieval, Azure batching and failure behaviour, grounding boundaries, execution policy, URL construction, authentication redaction, network/SSRF controls and atomic state replacement.

The design rule is simple:

> **The model is intentionally the least trusted part of the system.**

## Licence

See [LICENSE](LICENSE).
