# .NET MCP API Tester

`dotnet-mcp-api-tester` is a local .NET MCP server for Claude and Codex. Point it at an OpenAPI definition and it parses the contract into meaningful operation, schema and security evidence, embeds that evidence into an in-memory vector index using Azure AI, and exposes MCP tools that let an agent understand and safely test the API.

The MCP server itself makes the HTTP calls, so it can test localhost, internal, staging or public APIs that you are authorised to test. Nothing is persisted. Restart the server and the loaded contract, derived vector state, runtime authentication and policy changes disappear.

The design is deliberately simple: **the OpenAPI contract decides what is true, deterministic code decides what is mechanically testable and safe, and the LLM handles the reasoning that remains.**

## What it does

- Loads OpenAPI / Swagger from a local file or URL.
- Normalises missing operation IDs so every operation has a stable identity.
- Exposes MCP tools to list and describe operations.
- Generates deterministic test cases from schema constraints, including boundaries and malformed inputs.
- Turns operations, component schemas and security schemes into semantic RAG evidence rather than arbitrary text chunks.
- Uses Azure OpenAI embeddings and chat for the full grounded RAG path; a deterministic local fallback is available when Azure is not configured.
- Keeps vector state in memory only.
- Builds dry-run requests before live execution.
- Applies method, target, request-size, response-size, localhost/private-network and SSRF controls before the server makes HTTP calls.

## Requirements

- .NET 8 SDK
- Codex CLI or Claude Code
- Optional for full AI-backed RAG: an Azure OpenAI resource with a chat deployment and an embedding deployment
- Azure CLI if you want keyless local Azure authentication

Build it first:

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

## Azure AI setup

For local development I use Azure CLI credentials rather than putting an API key in the repository:

```powershell
az login

$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='AzureCli'
```

On Azure, use `ManagedIdentity` instead of `AzureCli`. An API-key mode is also supported, but do not commit keys or tokens.

If Azure is not configured, the server still starts and uses deterministic local embedding/chat fallbacks. That is useful for development, but Azure OpenAI is the intended path for the full RAG experience.

## Run it directly

```bash
dotnet run --project ./ApiTester.McpServer/ApiTester.McpServer.csproj
```

It is a stdio MCP server, so normally Codex or Claude Code starts this process for you.

## Connect it to Codex

From the repository root:

```bash
codex mcp add api-tester -- dotnet run --project ./ApiTester.McpServer/ApiTester.McpServer.csproj
```

Check it is registered:

```bash
codex mcp list
```

Codex also supports MCP configuration in `~/.codex/config.toml` or a project-scoped `.codex/config.toml`:

```toml
[mcp_servers.api-tester]
command = "dotnet"
args = ["run", "--project", "./ApiTester.McpServer/ApiTester.McpServer.csproj"]
```

If you use a project-scoped config, run Codex from the repository so the relative project path resolves correctly.

## Connect it to Claude Code

From the repository root:

```bash
claude mcp add api-tester -- dotnet run --project ./ApiTester.McpServer/ApiTester.McpServer.csproj
```

Check it is registered:

```bash
claude mcp get api-tester
claude mcp list
```

Use `--scope user` if you want the server available across Claude Code projects instead of only the current project.

## Typical workflow

Once the MCP server is connected, ask the agent to do the following:

1. Load an OpenAPI file or URL with `api_import_open_api`.
2. Inspect the contract with `api_list_operations` and `api_describe_operation`.
3. Generate deterministic edge cases with `api_generate_test_plan`.
4. Build the semantic in-memory index with `api_rag_index`.
5. Ask grounded questions with `api_rag_ask`.
6. Inspect the execution policy with `api_get_policy`.
7. Set a target base URL or bearer token if the contract does not already contain what is needed.
8. Use `api_call_operation` in dry-run mode first.
9. Only enable live execution for an API and methods you intentionally want the agent to test.

Example prompt:

> Load `./openapi.json`, show me the available operations, generate edge cases for `getWidgetById`, index the contract, explain the documented response behaviour using only retrieved OpenAPI evidence, then dry-run the request. Do not make a live HTTP call until I approve it.

## Testing localhost and internal APIs

The server can call localhost and private-network targets, but the safe default is to block them. This is intentional because MCP tools are agent-callable.

Policy mutation is also disabled by default. For a supervised local test where you explicitly want the connected agent to loosen the policy, start the MCP process with:

```powershell
$env:APITESTER_MCP_ALLOW_POLICY_MUTATION='true'
```

Then allow only the base URL and methods you actually need, for example `http://127.0.0.1:5055` and `GET`. Link-local metadata addresses remain blocked.

Leave `APITESTER_MCP_ALLOW_POLICY_MUTATION` unset for normal use.

## Local smoke API

`ApiTester.SmokeApi` is a small controlled API for exercising the MCP server without pointing it at a real system:

```bash
dotnet run --project ./ApiTester.SmokeApi/ApiTester.SmokeApi.csproj
```

Use its generated OpenAPI document as a safe end-to-end target while developing or demonstrating the server.

## State model

There is no database, run-history store, tenant model, web UI or hosted SaaS layer in this branch. The current OpenAPI contract, vector index, runtime target, bearer token and execution policy all live inside the MCP process. Stop the process and that state is gone.

That is intentional: this repository is an MCP API-testing tool, not a platform around one.

## Licence

See [LICENSE](LICENSE).
