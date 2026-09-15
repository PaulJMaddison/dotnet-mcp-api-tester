# Installation

## Install the MCP server

Install the .NET global tool from NuGet:

```bash
dotnet tool install --global PaulJMaddison.DotnetMcpApiTester
```

The tool command is `mcp-api-tester` and requires the .NET 8 runtime.

## Configure Azure AI

The MCP process owns the Azure credential. The agent does not receive Azure keys or tokens.

Recommended local authentication uses Azure CLI / Microsoft Entra ID:

```powershell
az login
$env:AZURE_OPENAI_ENDPOINT='https://<resource>.openai.azure.com/openai/v1/'
$env:AZURE_OPENAI_CHAT_DEPLOYMENT='<chat-deployment>'
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT='<embedding-deployment>'
$env:AZURE_OPENAI_AUTHENTICATION='DefaultAzureCredential'
$env:AZURE_OPENAI_CREDENTIAL_SOURCE='AzureCli'
```

For Azure-hosted execution, use `ManagedIdentity` as the credential source. API-key authentication is also supported by setting `AZURE_OPENAI_AUTHENTICATION=ApiKey` and `AZURE_OPENAI_API_KEY` in the local MCP process environment. Never commit or send credentials through an agent prompt.

## Register with Claude Code

```bash
claude mcp add --scope user api-tester -- mcp-api-tester
claude mcp list
```

## Register with Codex

```bash
codex mcp add api-tester -- mcp-api-tester
codex mcp list
```

If a client does not inherit the shell environment, configure the same variables in that client's MCP server environment. The Azure credential remains local to the MCP process.

## First load and prompts

Ask the connected agent:

```text
Load https://petstore3.swagger.io/api/v3/openapi.json
Which endpoint retrieves a pet by ID?
What parameters does it require?
Generate edge cases for the petId parameter.
Test the required parameter, wrong types, zero, negative values and numeric boundaries.
Show me the authentication requirements.
Call this safe GET operation and tell me what came back.
```

The OpenAPI semantic vector index is held in memory. No Pinecone, pgvector, Elasticsearch, or separate vector database is required. Restarting the MCP process discards the derived index.
