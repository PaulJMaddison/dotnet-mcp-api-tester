# MCP agent development workflow

Dotnet MCP API Tester is designed to sit inside an AI-assisted development loop rather than requiring a developer to leave their coding agent and manually recreate API tests elsewhere.

The coding agent understands the repository. API Tester specialises in understanding and exercising the API boundary.

## The division of responsibility

A useful mental model is:

| Concern | Owner |
|---|---|
| Source code, intent and implementation changes | Claude / Codex / developer |
| What the API contract actually says | OpenAPI |
| Mechanical boundary cases | Deterministic C# |
| Semantic reasoning over documented evidence | LLM |
| What network actions are permitted | Execution policy |
| What really happened at runtime | HTTP evidence / test results |

The LLM is deliberately not the source of truth for the API contract and is not the authority for network execution.

## Typical feature-development loop

```text
1. Developer or coding agent changes API code
                  |
2. Build/unit tests pass
                  |
3. Start the service locally or in an approved environment
                  |
4. API Tester imports/refreshed OpenAPI
                  |
5. Deterministic contract analysis generates edge cases
                  |
6. Structured RAG retrieves relevant operation/schema/security evidence
                  |
7. AI adds semantic analysis where useful
                  |
8. API Tester dry-runs requests through execution policy
                  |
9. Approved requests execute
                  |
10. Runtime behaviour is compared with the documented contract
                  |
11. Findings return to the coding agent
                  |
12. Agent fixes the implementation and reruns qualification
```

This turns API testing into a reusable capability rather than another prompt that has to be reinvented for every repository.

## Registering the MCP server

The server uses MCP over stdio.

The underlying command is:

```bash
dotnet run --project /absolute/path/to/dotnet-mcp-api-tester/ApiTester.McpServer
```

Configure that command in your MCP-capable client using the client's normal local/stdio server configuration.

### Codex example

```toml
[mcp_servers.api_tester]
command = "dotnet"
args = ["run", "--project", "/ABSOLUTE/PATH/dotnet-mcp-api-tester/ApiTester.McpServer"]
```

Restart/reload the MCP client after changing its configuration and confirm the API Tester tools are visible before relying on it in a development workflow.

### Claude and other MCP clients

Use the equivalent local stdio MCP registration in the client you use. The command/arguments remain the same; only the client's configuration format changes.

Do not place Azure access tokens or API keys in MCP configuration. Prefer keyless identity for Azure-hosted models.

## A useful first prompt

Once the server is connected, a developer can give the coding agent a high-level task rather than manually calling tools one by one:

```text
Use the API Tester MCP server to qualify the API I am working on.

- Import the OpenAPI contract.
- Show the operations and important constraints.
- Generate deterministic boundary tests from the contract.
- Index structured API evidence for grounded reasoning.
- Identify important semantic/integration scenarios without inventing undocumented behaviour.
- Show the current execution policy.
- Dry-run representative requests first.
- Ask before enabling any live execution.
- Execute only the explicitly approved cases.
- Compare runtime results with OpenAPI.
- Separate deterministic findings from AI reasoning in the final report.
```

The exact tool sequence can be chosen by the agent. The important point is that the hard capabilities remain inside the tester rather than being improvised through shell commands.

## Why deterministic generation comes first

There is no advantage in asking a language model to rediscover constraints the contract already provides exactly.

For example:

```yaml
id:
  type: integer
  minimum: 1
  maximum: 9999
```

The deterministic generator can derive:

```text
missing
0
1
2
9998
9999
10000
negative values
integer extremes
wrong type
```

The LLM can then spend its reasoning budget on things that are not directly encoded by the schema, such as likely integration mistakes, business-level scenarios, or interpreting unusual runtime failures.

## Structured RAG

OpenAPI is parsed into semantic evidence units rather than arbitrary character slices.

### Operation evidence

Keeps together:

- method;
- path;
- effective operation identity;
- path/operation parameters;
- request body;
- documented security;
- documented responses.

### Schema evidence

Keeps component constraints and property structure together.

### Security evidence

Keeps authentication/security scheme definitions independently retrievable.

The question is embedded, evidence is retrieved only for the selected project, and the model receives the retrieved chunks inside an explicit untrusted-data boundary.

If there is no indexed evidence for a project, the RAG service does not call the model.

## Safety and authority

MCP makes agent orchestration easy, but an agent request is not equivalent to permission.

The execution path can enforce:

- dry-run first;
- method allowlists;
- base URL allowlists;
- localhost/private-network restrictions;
- SSRF checks;
- request/response limits;
- timeouts;
- restricted process-start capability for policy mutation.

By default, an MCP agent cannot loosen execution policy through MCP.

For a deliberately supervised local demo/development process, the human can opt in before process startup using:

```text
APITESTER_MCP_ALLOW_POLICY_MUTATION=true
```

That setting is a capability gate, not a secret. It should not be enabled merely for convenience in unattended environments.

## Azure-backed reasoning

The Azure path can provide both semantic embeddings and grounded chat.

Recommended identity choices:

### Local developer machine

Use an authenticated Azure CLI identity and select the Azure CLI credential path when you want deterministic credential-source selection.

### Azure-hosted production

Use Managed Identity with least-privilege RBAC on the relevant Azure AI resource.

### Secrets

Do not commit:

- access tokens;
- API keys;
- client secrets;
- database passwords;
- third-party credentials.

The Azure endpoint and deployment names are configuration metadata, not authentication credentials.

## Pre-PR workflow

A useful future developer workflow is:

```text
feature branch
   |
restore/build/unit tests
   |
start candidate API
   |
API qualification
   |
contract-derived failures
semantic findings
runtime/contract discrepancies
   |
PR evidence
```

The qualification report could eventually be emitted as Markdown, JUnit, SARIF or another CI-friendly format while retaining the same separation between deterministic evidence and AI reasoning.

## Third-party API qualification

The same workflow is useful when a team consumes rather than owns an API:

```text
OpenAPI from supplier/vendor
       |
import
       |
operation + schema discovery
       |
deterministic constraints
       |
grounded explanation
       |
safe read-only qualification
       |
integration notes for the development team
```

Execution should always respect the target service's terms, rate limits and authorization requirements. The tool is not intended for unauthorized probing or load testing.

## What the agent should not do

A good agent workflow should avoid these patterns:

- inventing undocumented parameters or status codes;
- treating an API description as trusted prompt instructions;
- enabling unrestricted network access just to make a test pass;
- performing destructive operations without explicit approval;
- using an LLM to generate boundaries already specified by OpenAPI;
- silently falling back to local embeddings/chat while claiming Azure-backed qualification;
- mixing project evidence across unrelated APIs.

## Development philosophy

The core principle is deliberately simple:

> The contract decides what is true, deterministic code decides what is mechanically testable and safe, and the LLM handles the reasoning that remains.

That makes the MCP server useful to an AI coding agent without making the model responsible for truth, safety or evidence.