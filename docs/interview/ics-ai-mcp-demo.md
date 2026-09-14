# ICS.AI MCP-first interview demo

## Recommendation

Use the MCP server as the primary demo. Keep `ApiTester.Web` as the fallback UI.

The point of the demo is not "look, I can call an LLM". It is:

> An agent can connect to a controlled API-testing capability, import a real OpenAPI contract, derive deterministic boundary cases from the contract, use grounded AI for the reasoning that remains, and execute only what application policy permits.

## Architecture

```text
Codex / Claude / other MCP client
                |
                | stdio MCP
                v
       ApiTester.McpServer
        /       |        \
OpenAPI     grounded AI   execution policy
   |            |              |
   v            v              v
structured   Azure chat/     real HTTP
operation/   embeddings      calls/results
schema/auth      |
evidence         v
   |         answer + visible evidence
   v
constraint-driven test cases
```

The important boundary is:

- OpenAPI decides what is documented/true.
- deterministic C# derives mechanical boundary cases;
- Azure AI handles semantic reasoning over retrieved evidence;
- deterministic application policy decides what may execute.

## Local deterministic target

The repository includes `ApiTester.SmokeApi`. For the interview branch it has deliberate constraints and failure modes so the demo does not depend on an external public API.

Start it in one terminal:

```powershell
$env:ASPNETCORE_URLS="http://127.0.0.1:5055"
dotnet run --project ApiTester.SmokeApi
```

Its OpenAPI document is then available at:

```text
http://127.0.0.1:5055/openapi.json
```

The fixture includes:

- `getWidgetById`
  - integer `id`
  - minimum 1
  - maximum 9999
  - optional boolean `includeTags`
  - 200 / 400 / 404 responses
- `createWidget`
  - required request body
  - referenced component schema
  - required `id`, `name`, `status`
  - integer min/max
  - string min/max length and regex pattern
  - status enum
  - tags array maxItems

## Add the server to Codex

Codex can consume user-provided MCP servers. In `~/.codex/config.toml`, configure the local stdio server with the absolute project path:

```toml
[mcp_servers.api_tester]
command = "dotnet"
args = ["run", "--project", "C:\\ABSOLUTE\\PATH\\dotnet-mcp-api-tester\\ApiTester.McpServer"]
```

Restart/reload the Codex client after changing MCP configuration and confirm the API Tester tools are visible before the interview.

Do not put Azure keys or access tokens in this file. Let the server inherit the local Azure/AI environment or use the keyless `DefaultAzureCredential` path once merged.

## Primary demo script

Give Codex this high-level request rather than manually invoking every tool:

```text
Use the API Tester MCP server to qualify the local fixture API.

1. Create a project called "ICS Interview Demo".
2. Import http://127.0.0.1:5055/openapi.json.
3. Show me the available operations.
4. Generate the deterministic test plan for getWidgetById and explain which cases came directly from OpenAPI constraints.
5. Generate the deterministic test plan for createWidget and show the min/max, enum, pattern, required-field and array-boundary cases.
6. Index the project for RAG.
7. Ask the grounded AI what the documented failure modes are for getWidgetById and show the retrieved evidence separately from the generated answer.
8. Show the current execution policy before making any network call.
9. Keep execution in dry-run mode first and show the request that would be sent for id=0 and id=1.
10. Only after showing the policy boundary, enable the minimum local permissions required for this fixture and execute permitted cases.
11. Summarise what failed, what passed, and which conclusions came from deterministic contract checks versus AI reasoning.

Do not invent parameters or behaviour that is absent from the OpenAPI contract.
```

The agent should discover and compose the MCP tools itself. That is part of the demo.

## Safety moment to show deliberately

The fixture uses localhost. Default policy is intentionally restrictive.

Do not silently disable the controls before the demo. Show them.

A good sequence is:

1. import/analyse/index while execution remains safe;
2. call the policy inspection tool;
3. run an operation in dry-run mode;
4. explain that the model cannot grant itself network access;
5. explicitly allow only `http://127.0.0.1:5055`, the required methods, and local access for the fixture;
6. execute the selected cases;
7. reset runtime/policy at the end.

That turns a limitation into one of the strongest architectural points in the walkthrough.

## Code walkthrough after the live demo

Show these files in this order:

1. `ApiTester.McpServer/Rag/OpenApiEvidenceBuilder.cs`
   - operation/schema/security evidence boundaries;
   - path-level + operation-level parameters kept together;
   - auth/responses/request body kept with the operation;
   - component schemas separately retrievable.

2. `ApiTester.McpServer/Services/OpenApiConstraintTestGenerator.cs`
   - no LLM required for documented constraints;
   - min/max and just-inside/just-outside boundaries;
   - enum valid/invalid/case variants;
   - required/optional/null;
   - min/max length;
   - arrays;
   - email/uuid/date/date-time/URI formats;
   - referenced request-body schemas resolved.

3. `ApiTester.McpServer/Tools/ApiAssistTools.cs`
   - intentionally thin MCP adapter over normal C# domain logic.

4. `ApiTester.McpServer/Tools/RagTools.cs`
   - project/tenant context boundary;
   - structured evidence indexing;
   - visible evidence returned independently from the model answer.

5. `ApiTester.Rag/Answering/RagAnswerService.cs`
   - question -> embedding -> project-scoped retrieval -> evidence -> model;
   - no evidence means no model call.

6. `ApiTester.Rag/Prompting/RagPromptBuilder.cs`
   - retrieved documentation treated as untrusted data;
   - no invented endpoints/parameters/auth/errors.

7. `ApiTester.AI/Azure/AzureOpenAiTransport.cs`
   - authentication, timeout, retry, Retry-After, response bounds, circuit breaker, safe diagnostics and telemetry.

8. `ApiTester.McpServer/Tools/ExecuteTools.cs` / `PolicyTools.cs`
   - agent intent is not authority;
   - execution is governed by deterministic policy.

9. `ApiTester.Web.UnitTests/OpenApiStructuredAnalysisTests.cs`
   - finish with evidence that structure and constraints are mechanically protected.

## The line to use

> The contract decides what is true, deterministic code decides what is mechanically testable and safe, and the LLM handles the reasoning that remains.

## Web fallback

If MCP configuration fails on the interview machine/client, do not debug Codex configuration for ten minutes in front of the panel.

Use the repository's existing Web/demo path instead and explain that it invokes the same application services. The architecture discussion remains identical.

The fallback order should be:

1. `ApiTester.DemoWorkshop` for a deterministic console run;
2. `ApiTester.Web` if they specifically want a conventional UI/API surface;
3. source walkthrough + tests if live Azure is unavailable.
