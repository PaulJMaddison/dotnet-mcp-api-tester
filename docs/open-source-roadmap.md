# Open-source roadmap

This project began as an API testing side project and has evolved into an MCP-native API qualification tool for AI-assisted development.

The current goal is to make the useful parts easy for any development team to adopt without coupling the project to one editor, one coding agent, or one model provider.

## Product direction

A concise positioning statement is:

> **Give your coding agent an API test engineer.**

The core user experience should become:

```text
install / configure MCP server
        |
give agent an OpenAPI URL or file
        |
"Qualify this API"
        |
contract understanding
+ deterministic edge cases
+ grounded AI reasoning
+ controlled execution
        |
report / findings / evidence
```

## What should remain core

The following ideas are architectural rather than vendor-specific and should remain central:

- OpenAPI is the contract/source of truth;
- deterministic generation owns mechanically derivable edge cases;
- structured evidence is preferred to arbitrary OpenAPI text chunking;
- the LLM is used for semantic reasoning, not contract truth;
- execution is behind deterministic policy;
- MCP provides the agent-facing capability surface;
- evidence and model-generated interpretation stay separable;
- project/tenant context must remain isolated;
- local/offline fallbacks must be explicit rather than silently masquerading as cloud AI.

## Near-term release work

Before calling the project a polished public release, prioritise:

1. **Mechanical verification**
   - full restore/build/test gate;
   - public API qualification against more than the local fixture;
   - secret scanning;
   - reproducible clean-clone setup.

2. **MCP installation experience**
   - concise client configuration examples;
   - absolute-path and Docker options;
   - clear startup diagnostics;
   - a tool inventory command/example;
   - one-command local smoke fixture.

3. **First-run experience**
   - import a small public OpenAPI contract;
   - generate a deterministic plan;
   - run in dry-run mode;
   - optionally enable Azure-backed embeddings/chat;
   - show evidence and findings in under five minutes.

4. **Documentation**
   - architecture diagram showing agent/MCP/contract/RAG/policy boundaries;
   - security/authority model;
   - examples for owned APIs and third-party APIs;
   - troubleshooting for common MCP and Azure identity issues.

5. **Release hygiene**
   - versioning/release notes;
   - license review;
   - CONTRIBUTING.md;
   - Code of Conduct if community contribution grows;
   - issue templates;
   - security reporting instructions.

## Packaging options

The project currently runs naturally as a local .NET stdio MCP server.

Useful distribution options to evaluate:

### .NET tool

A packaged `dotnet tool` could provide a simple command such as:

```text
apitester-mcp
```

This would make client configuration much easier than pointing at a cloned project path.

### Docker image

A versioned container could make CI and isolated developer environments reproducible.

The MCP transport and local-network requirements need to be designed carefully so containerisation does not weaken the SSRF/network policy model.

### Release binaries

Self-contained binaries for common platforms could remove the requirement for users to install a matching .NET SDK.

### Hosted/remote MCP

A hosted service could eventually be useful for teams, but it introduces a substantially different trust model: authentication, tenant isolation, secret management, target-network access and execution approval would all need production-grade treatment.

Local stdio remains the safest default for an initial open-source release.

## Agent/client support

The implementation should avoid depending on agent-specific behaviour.

Target experience:

- Codex;
- Claude / Claude Code;
- other MCP-capable coding agents;
- direct MCP clients/harnesses;
- REST/UI workflows for teams that do not use MCP.

Documentation can provide client-specific setup examples, while the server contract remains standard MCP.

## Model/provider independence

Azure OpenAI is a strong production path, but model/provider choice should stay behind interfaces.

Potential future providers can implement the same chat/embedding abstractions without changing:

- OpenAPI parsing;
- evidence construction;
- deterministic case generation;
- execution policy;
- vector-store contracts;
- MCP tools.

Provider independence matters because the useful IP in this project is the testing/control workflow, not a dependency on a particular LLM vendor.

## Larger API estates

Large production OpenAPI contracts require additional work beyond simply raising file-size limits.

Useful directions include:

- operation/tag/path selection before indexing;
- incremental indexing when a contract changes;
- batching embeddings;
- embedding cache keyed by content hash and model/deployment;
- durable vector stores;
- progress telemetry;
- cancellation/restartability;
- selective qualification of changed operations;
- contract-diff-driven retesting.

The ideal workflow for a very large API is not necessarily "embed every operation on every run".

## CI / PR integration

A strong open-source evolution would be a non-interactive qualification command that can run in CI.

Possible outputs:

- Markdown summary;
- JSON evidence bundle;
- JUnit XML;
- SARIF;
- GitHub/other SCM PR comment;
- artifact containing deterministic cases and runtime evidence.

A CI mode should be able to run without an LLM. AI reasoning should be an optional enhancement, not a prerequisite for deterministic contract testing.

## Qualification profiles

Teams could define reusable policies such as:

```text
local-development
staging-readonly
staging-controlled-write
third-party-readonly
ci-dry-run
```

Each profile could specify:

- allowed hosts;
- allowed methods;
- dry-run/live mode;
- timeout limits;
- request/response limits;
- localhost/private-network policy;
- approval requirements.

This keeps safety policy versionable without asking the model to make authorization decisions.

## Reporting and developer feedback

A useful qualification report should distinguish at least three kinds of finding:

1. **Contract-derived**
   - exact OpenAPI constraints and deterministic boundary results.

2. **Runtime evidence**
   - actual HTTP status/headers/body/schema behaviour.

3. **AI reasoning**
   - semantic interpretation, suspected integration risks, explanations and suggested follow-up tests.

Mixing these together would make the report harder to trust.

## Community contribution opportunities

Good contribution areas include:

- additional OpenAPI formats/edge cases;
- deterministic generators for more schema keywords;
- alternative vector stores;
- model/embedding providers;
- MCP client setup guides;
- new output/report formats;
- CI integrations;
- safe authentication helpers for test environments;
- contract-diff analysis;
- performance work on large API estates.

## Non-goals

The project should not become:

- an unrestricted network scanner;
- a load-testing or denial-of-service tool;
- a replacement for authorization to test third-party systems;
- a system where the LLM can grant itself execution permission;
- a prompt-only wrapper around `curl`;
- a system that claims model-generated behaviour is part of an API contract.

## Release principle

The project is most useful when it gives an agent **more capability without giving the model more authority**.

That principle should remain the standard for future features.