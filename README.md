# .NET MCP API Tester

A local .NET MCP server for Claude and Codex.

Point it at an OpenAPI / Swagger definition. It parses the contract into meaningful **operation, schema and security evidence**, embeds that evidence into an **in-memory vector index using Azure AI**, and exposes MCP tools that let a coding agent understand and safely test the API.

The MCP server itself makes the HTTP calls, so it can test localhost, internal, staging or public APIs that you are authorised to test. **Nothing is persisted.** There is no database, web UI, SaaS backend or stored test-run history. Restart the MCP process and the loaded API definition, vector index, runtime auth and policy state disappear.

> **The OpenAPI contract decides what is true, deterministic code decides what is mechanically testable and safe, and the LLM handles the reasoning that remains.**

## How it works

```text
OpenAPI / Swagger definition
          ↓
parse operations + schemas + security
          ↓
custom semantic evidence builder
          ↓
Azure OpenAI embeddings (batched)
          ↓
in-memory vector index
          ↓
Claude / Codex via MCP
   ├─ list / describe operations
   ├─ semantic contract search
   ├─ grounded Azure AI questions
   ├─ deterministic edge-case generation
   └─ safe direct HTTP execution
          ↓
localhost / dev / staging / public API
```

The RAG implementation deliberately does **not** split OpenAPI JSON every N characters. `OpenApiEvidenceBuilder` keeps domain boundaries intact: an operation stays an operation, a component schema stays a schema and a security scheme stays a security scheme.

The vector store is intentionally in-process. OpenAPI is the source of truth; vectors are disposable derived state. That means no Pinecone, Elasticsearch, PostgreSQL/pgvector or other vector infrastructure is required.

## Requirements

- .NET 8 SDK
- Codex CLI or Claude Code
- Azure OpenAI chat deployment
- Azure OpenAI embedding deployment
- Azure credentials (Azure CLI / managed identity / API key / bearer token)

Azure AI is required. This project intentionally has no mock, local or deterministic AI fallback.

## Azure AI configuration

Azure authentication belongs to the **local MCP process**, not to the model. Claude or Codex never needs to receive your Azure credential and there is deliberately no MCP tool for setting an Azure API key or bearer token.

### Recommended for local development: Azure CLI / Microsoft Entra ID

Authenticate once with Azure CLI:

```powershell
az login
```

The signed-in identity must have permission to use the Azure OpenAI resource. Then configure only the resource/deployment information and tell the MCP process to use the Azure CLI identity:

```powershell
$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='AzureCli'
```

The flow is:

```text
Claude / Codex
      ↓ MCP stdio
API Tester MCP process
      ↓ Azure.Identity / Azure CLI credential
Azure OpenAI
```

The model never sees the credential.

For Azure-hosted execution, use managed identity instead:

```powershell
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='ManagedIdentity'
```

### Alternative: Azure OpenAI API key

For users who do not use Azure CLI/RBAC, API-key authentication is also supported:

```powershell
$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='ApiKey'
$env:AZURE_OPENAI_API_KEY='<your key>'
```

Keep the real key in the local process environment or an MCP-client secret/environment configuration. Never commit it and never paste it into a Claude/Codex prompt.

If required configuration is missing or invalid, the MCP process exits before starting stdio and writes actionable setup guidance to **stderr**. It never echoes configured keys or tokens.

## Build and test

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

## Connect it to Codex

From the repository root:

```bash
codex mcp add api-tester -- dotnet run --project ./ApiTester.McpServer/ApiTester.McpServer.csproj
```

Then check the registration:

```bash
codex mcp list
```

Equivalent Codex config:

```toml
[mcp_servers.api-tester]
command = "dotnet"
args = ["run", "--project", "./ApiTester.McpServer/ApiTester.McpServer.csproj"]
```

Start Codex from the repository when using the relative project path. If your MCP client does not inherit the shell environment, configure the non-secret Azure settings and any API key in that client's MCP process environment rather than passing credentials through chat.

## Connect it to Claude Code

From the repository root:

```bash
claude mcp add api-tester -- dotnet run --project ./ApiTester.McpServer/ApiTester.McpServer.csproj
```

Check it:

```bash
claude mcp get api-tester
claude mcp list
```

Use `--scope user` if you want the MCP server available across Claude Code projects rather than only the current project. As with Codex, credentials belong to the spawned MCP process environment or Azure CLI identity, never to an MCP prompt/tool call.

## Core MCP workflow

Loading an API is one operation. `api_load_open_api` downloads or reads the definition, parses it, creates semantic evidence, gets Azure embeddings and swaps in the new in-memory index only after indexing succeeds.

Typical flow:

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

Useful prompt:

> Load `./openapi.json`. Show me the available operations, find the endpoint for retrieving orders, generate the edge cases for that operation and dry-run the requests. Use only the OpenAPI contract as documented truth and do not make a live call until I approve it.

Another example:

> Load this Swagger definition and test the documented boundary cases for `getWidgetById`. Explain any mismatch between the contract and the actual response.

## Safety

Execution starts in **dry-run** mode and live calls are deny-by-default until an allowed base URL is configured. Localhost and private networks are blocked by default as well.

Policy mutation through MCP is disabled unless a human enables it before process startup:

```powershell
$env:APITESTER_MCP_ALLOW_POLICY_MUTATION='true'
```

Use that only for a supervised development session. Then allow only the target URL and HTTP methods you actually want the agent to exercise. Link-local metadata addresses remain blocked.

Target-API bearer tokens and base-URL overrides live only in the process. Azure AI credentials are also process-start configuration and are never exposed through MCP tools. Nothing is stored anywhere.

## Why in memory?

An API definition normally produces hundreds or a few thousand evidence chunks, not millions. A linear in-memory cosine-similarity scan is therefore simple, fast and cheap for this use case.

More importantly, the vector index is not valuable state. It can always be recreated from the OpenAPI contract. Persisting it would add hosting cost, credentials, network I/O, patching, backup and additional security surface without providing much value to a local developer MCP tool.

## Licence

See [LICENSE](LICENSE).
