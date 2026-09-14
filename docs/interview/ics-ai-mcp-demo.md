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
- deterministic application policy decides what may execute;
- the MCP agent cannot loosen policy at all unless a human enabled that capability before the server process started.

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

For a repeatable MCP harness run, start the server process with the human capability gate and Azure keyless settings already present in your shell, then run:

```powershell
$env:APITESTER_MCP_ALLOW_POLICY_MUTATION="true"
dotnet run --project ApiTester.DemoWorkshop -c Release -- --full-demo `
  --spec http://127.0.0.1:5055/openapi.json `
  --question "What are the documented boundaries and failure modes for getWidgetById?"
```

The harness lists and describes operations, prints both deterministic plans, performs structured RAG, shows dry-run requests, scopes live permission to the one local fixture, executes IDs 0, 1 and 10000, and resets runtime policy. To prove the default gate independently, unset the process flag and run `ApiTester.DemoWorkshop` with `--policy-check-only`.

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

For the live interview fixture only, if you want the agent to be able to loosen the execution policy after it shows you the initial denial, explicitly opt in at process launch:

```toml
[mcp_servers.api_tester]
command = "dotnet"
args = ["run", "--project", "C:\\ABSOLUTE\\PATH\\dotnet-mcp-api-tester\\ApiTester.McpServer"]
env = { APITESTER_MCP_ALLOW_POLICY_MUTATION = "true" }
```

That flag is not a secret. It is a human-controlled capability switch. Without it, `ApiSetPolicy` fails closed.

Restart/reload the Codex client after changing MCP configuration and confirm the API Tester tools are visible before the interview.

Do not put Azure keys or access tokens in this file. Let the server inherit the local Azure/AI environment or use the keyless `DefaultAzureCredential` path once merged.

## Primary demo script

Give Codex this high-level request rather than manually invoking every tool:

```text
Use the API Tester MCP server to qualify the local fixture API.

1. Create a project called "ICS Interview Demo".
2. Inspect the current execution policy and tell me whether MCP policy mutation was enabled by the human at process startup.
3. Try to import http://127.0.0.1:5055/openapi.json without weakening the policy first. I expect localhost SSRF protection to block it.
4. Show me the exact reason it was blocked.
5. If and only if the process-level mutation capability is enabled, change the minimum policy required for this fixture:
   - keep dryRun=true;
   - allow only http://127.0.0.1:5055;
   - allow GET and POST;
   - allow localhost for this fixture only.
6. Import the OpenAPI document successfully.
7. Show me the available operations.
8. Generate the deterministic test plan for getWidgetById and explain which cases came directly from OpenAPI constraints.
9. Generate the deterministic test plan for createWidget and show the min/max, enum, pattern, required-field and array-boundary cases.
10. Index the project for RAG.
11. Ask the grounded AI what the documented failure modes are for getWidgetById and show the retrieved evidence separately from the generated answer.
12. While dryRun is still true, show the request that would be sent for id=0 and id=1.
13. Ask me before changing dryRun to false.
14. After approval, execute a small set of permitted cases, including id=0, id=1 and id=10000.
15. Summarise what failed, what passed, and which conclusions came from deterministic contract checks versus AI reasoning.
16. Reset runtime/policy to safe defaults at the end.

Do not invent parameters or behaviour that is absent from the OpenAPI contract.
```

The agent should discover and compose the MCP tools itself. That is part of the demo.

## Safety moment to show deliberately

The localhost import should fail on the first attempt. That is intentional.

The useful conversation is:

1. `ApiGetPolicy` shows deny-by-default execution and `blockLocalhost=true`;
2. the attempted localhost OpenAPI import is blocked by the SSRF guard;
3. `mcpPolicyMutationEnabled` shows whether the human pre-authorized policy changes at process startup;
4. only in the interview-specific process, after human opt-in, can the agent scope policy down to one base URL and GET/POST;
5. execution remains dry-run until a second explicit decision changes it;
6. `ApiResetRuntime` returns everything to safe defaults.

The architectural statement is therefore accurate:

> The model cannot create new authority. The process decides whether policy mutation is even available, and the policy decides what network actions are permitted.

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

8. `ApiTester.McpServer/Services/McpSafetyOptions.cs` + `PolicyTools.cs` + `ExecuteTools.cs`
   - human capability gate at process startup;
   - agent intent is not authority;
   - execution remains deterministic policy.

9. `ApiTester.Web.UnitTests/OpenApiStructuredAnalysisTests.cs` + `McpPolicySafetyTests.cs`
   - finish with evidence that structure, constraints and authority boundaries are mechanically protected.

## The line to use

> The contract decides what is true, deterministic code decides what is mechanically testable and safe, and the LLM handles the reasoning that remains.

## Web fallback

If MCP configuration fails on the interview machine/client, do not debug Codex configuration for ten minutes in front of the panel.

Use the repository's existing Web/demo path instead and explain that it invokes the same application services. The architecture discussion remains identical.

The fallback order should be:

1. `ApiTester.DemoWorkshop` for a deterministic console run;
2. `ApiTester.Web` if they specifically want a conventional UI/API surface;
3. source walkthrough + tests if live Azure is unavailable.
