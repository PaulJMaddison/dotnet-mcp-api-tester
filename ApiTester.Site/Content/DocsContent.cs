namespace ApiTester.Site.Content;

public static class DocsContent
{
    public static DocsLibrary Current { get; } = Build();

    private static DocsLibrary Build()
    {
        var pages = new List<DocPage>
        {
            new(
                "getting-started",
                "Getting started",
                "Install the MCP client, connect a project, and run your first deterministic plan.",
                new List<DocSection>
                {
                    new(
                        "install-mcp",
                        "Install the MCP client",
                        "Use the MCP CLI to authenticate, configure your workspace, and verify connectivity.",
                        new List<string>
                        {
                            "Download the MCP client package for your OS.",
                            "Sign in and link the client to your ApiTester org.",
                            "Run the diagnostics command to verify outbound policy allowlists."
                        },
                        new CodeSnippet(
                            "Install and sign in",
                            "Authenticate the MCP client and verify access.",
                            "bash",
                            "apitester auth login\napitester doctor\napitester projects list"),
                        Array.Empty<DocCallout>()),
                    new(
                        "connect-project",
                        "Connect your first project",
                        "Projects map environments, policies, and OpenAPI imports into a single workspace.",
                        new List<string>
                        {
                            "Create a project and assign an owner team.",
                            "Define environments (staging, prod) with per-env secrets.",
                            "Set host allowlists before the first run to satisfy SSRF rules."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Tip",
                                "Keep environment secrets scoped to the env and rotate them through your secret manager of choice.")
                        }),
                    new(
                        "run-first-plan",
                        "Run your first deterministic plan",
                        "Generate a plan from OpenAPI, then execute it with a single command.",
                        new List<string>
                        {
                            "Import the OpenAPI document for the project.",
                            "Generate a deterministic plan from the spec.",
                            "Run the plan and review the run summary in the web UI."
                        },
                        new CodeSnippet(
                            "Generate + run",
                            "Create a deterministic plan and execute it.",
                            "bash",
                            "apitester openapi import --project api-staging --file openapi.json\napitester plans generate --project api-staging\napitester plans run --project api-staging --plan deterministic"),
                        Array.Empty<DocCallout>())
                }),
            new(
                "import-openapi",
                "Import OpenAPI",
                "Bring your OpenAPI contract into ApiTester so deterministic plans stay in sync.",
                new List<DocSection>
                {
                    new(
                        "import-methods",
                        "Choose an import method",
                        "Upload a file, pull from a URL, or connect a repository for scheduled refreshes.",
                        new List<string>
                        {
                            "Upload: best for local or one-off contracts.",
                            "URL import: pull from a hosted spec endpoint.",
                            "Repo sync: auto-refresh on branch merges."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "validate-contract",
                        "Validate the contract",
                        "ApiTester validates schemas, required fields, and response codes before generating plans.",
                        new List<string>
                        {
                            "Verify server URLs map to allowed hosts.",
                            "Confirm auth schemes are defined for secured operations.",
                            "Review any warnings surfaced for missing responses."
                        },
                        new CodeSnippet(
                            "Import via API",
                            "Trigger an OpenAPI import from CI or an integration.",
                            "bash",
                            "curl -X POST https://apitester.example.com/api/openapi/import \\\n  -H \"Authorization: Bearer $APITESTER_TOKEN\" \\\n  -H \"Content-Type: application/json\" \\\n  -d '{\"projectKey\":\"api-staging\",\"sourceUrl\":\"https://api.example.com/openapi.json\"}'"),
                        Array.Empty<DocCallout>()),
                    new(
                        "plan-generation",
                        "Generate deterministic plans",
                        "Use the latest contract to build deterministic suites, then add AI probes for gaps.",
                        new List<string>
                        {
                            "Pick which tags or paths become deterministic plans.",
                            "Add parameterization for required query/path values.",
                            "Promote AI suggestions into deterministic plans after review."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Reminder",
                                "Only deterministic plans gate releases. AI probes remain exploratory unless you promote them.")
                        })
                }),
            new(
                "policies-ssrf",
                "Policies + SSRF safety",
                "Define allowlists, block private ranges, and keep secrets out of run artifacts.",
                new List<DocSection>
                {
                    new(
                        "allowlists",
                        "Configure host allowlists",
                        "Allowlist domains and CIDRs per environment so outbound requests stay in policy.",
                        new List<string>
                        {
                            "Use exact hostnames for production APIs.",
                            "Allow staging CIDRs only for non-prod environments.",
                            "Keep metadata hosts blocked even in staging."
                        },
                        new CodeSnippet(
                            "Allowlist example",
                            "Define allowlists in project policy settings.",
                            "json",
                            "{\n  \"environment\": \"staging\",\n  \"allowedHosts\": [\"api.staging.example.com\"],\n  \"allowedCidrs\": [\"203.0.113.0/24\"]\n}"),
                        Array.Empty<DocCallout>()),
                    new(
                        "ssrf-guard",
                        "Understand the SSRF guard",
                        "ApiTester blocks loopback, RFC1918, and cloud metadata ranges by default.",
                        new List<string>
                        {
                            "Loopback and private ranges are denied automatically.",
                            "Cloud metadata IPs are blocked regardless of allowlist.",
                            "Requests failing SSRF rules are logged with the rule name."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "redaction",
                        "Redact sensitive data",
                        "Mask secrets in payloads before they hit run history, exports, or AI prompts.",
                        new List<string>
                        {
                            "Define JSONPath-based redaction rules.",
                            "Apply per-environment or per-project redaction policies.",
                            "Audit log entries always store redaction metadata."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Security",
                                "Redaction is enforced before AI grounding, so prompts never include masked data.")
                        })
                }),
            new(
                "running-test-plans",
                "Running test plans",
                "Execute deterministic suites or AI probes and capture evidence with every run.",
                new List<DocSection>
                {
                    new(
                        "deterministic",
                        "Run deterministic plans",
                        "Deterministic plans are repeatable, versioned, and required for release gates.",
                        new List<string>
                        {
                            "Select the plan set tied to your OpenAPI tag or path.",
                            "Run against a specific environment with scoped secrets.",
                            "Attach baseline comparisons to highlight regressions."
                        },
                        new CodeSnippet(
                            "Run a plan",
                            "Execute a deterministic plan from the CLI.",
                            "bash",
                            "apitester plans run --project api-staging --plan payments-deterministic"),
                        Array.Empty<DocCallout>()),
                    new(
                        "ai-probes",
                        "Layer in AI probes",
                        "AI probes explore edge cases without affecting deterministic baselines.",
                        new List<string>
                        {
                            "Generate probes from the same OpenAPI contract.",
                            "Review AI suggestions before promoting them.",
                            "Tag probes by risk or area to triage faster."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "gates",
                        "Gate releases with readiness checks",
                        "Use readiness gates to block deployments when deterministic plans fail.",
                        new List<string>
                        {
                            "Define SLA thresholds for latency and error rates.",
                            "Export pass/fail results to CI status checks.",
                            "Require approvals for flaky reruns."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Best practice",
                                "Keep AI probes out of gates until the scenario is deterministic and approved.")
                        })
                }),
            new(
                "run-history",
                "Viewing run history",
                "Track evidence, baselines, and annotations across every run.",
                new List<DocSection>
                {
                    new(
                        "timeline",
                        "Review the run timeline",
                        "Each run captures requests, responses, assertions, and artifacts.",
                        new List<string>
                        {
                            "Filter by environment, plan, or status.",
                            "Inspect failed assertions with raw payloads.",
                            "Download artifacts for compliance review."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "baselines",
                        "Compare baselines",
                        "Baselines highlight regressions by comparing current runs against a known good run.",
                        new List<string>
                        {
                            "Mark a run as baseline for a plan version.",
                            "Surface contract changes in diff view.",
                            "Annotate flaky behavior with owner notes."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "exports",
                        "Export evidence",
                        "Export reports with redacted payloads and audit metadata.",
                        new List<string>
                        {
                            "PDF exports for release sign-off.",
                            "JSON exports for archival storage.",
                            "Include AI explanations with references to source runs."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Compliance",
                                "Exports include hash identifiers for traceability across releases.")
                        })
                }),
            new(
                "persistence",
                "Persistence setup (SqlServer vs File)",
                "Choose the right storage backend for your environment and retention needs.",
                new List<DocSection>
                {
                    new(
                        "file-store",
                        "File-based storage",
                        "File persistence is the default for local evaluation and sandbox projects.",
                        new List<string>
                        {
                            "Stores runs and artifacts on local disk.",
                            "Best for local demos and proof-of-concept work.",
                            "Configure a retention period to avoid disk growth."
                        },
                        new CodeSnippet(
                            "File store settings",
                            "Configure file-based persistence.",
                            "json",
                            "{\n  \"Persistence\": {\n    \"Provider\": \"File\",\n    \"RootPath\": \"./data/run-history\",\n    \"RetentionDays\": 14\n  }\n}"),
                        Array.Empty<DocCallout>()),
                    new(
                        "sql-server",
                        "SqlServer storage",
                        "SqlServer is recommended for shared environments, longer retention, and reporting.",
                        new List<string>
                        {
                            "Run migrations before enabling in production.",
                            "Use an application user with least-privilege access.",
                            "Enable backups and retention policies at the DB level."
                        },
                        new CodeSnippet(
                            "SqlServer settings",
                            "Point ApiTester to a SqlServer instance.",
                            "json",
                            "{\n  \"Persistence\": {\n    \"Provider\": \"SqlServer\",\n    \"ConnectionString\": \"Server=sql01;Database=ApiTester;User Id=apitester;Password=***;\"\n  }\n}"),
                        Array.Empty<DocCallout>()),
                    new(
                        "migration",
                        "Migration and retention",
                        "Move from file to SqlServer by exporting run history and reimporting into the database.",
                        new List<string>
                        {
                            "Export run history as JSON from the file store.",
                            "Import using the persistence migration tool.",
                            "Validate retention policies after migration."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Note",
                                "Schedule database maintenance jobs to align with your compliance retention rules.")
                        })
                }),
            new(
                "ci-usage",
                "CI usage",
                "Run deterministic plans headlessly in CI with the MCP client.",
                new List<DocSection>
                {
                    new(
                        "headless",
                        "Authenticate headlessly",
                        "Use short-lived tokens and secrets injection to authenticate in CI.",
                        new List<string>
                        {
                            "Store tokens in your CI secret vault.",
                            "Rotate tokens on a schedule.",
                            "Scope tokens to specific projects and environments."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "pipeline",
                        "Pipeline example",
                        "Trigger imports, generate plans, and run deterministic suites in one workflow.",
                        new List<string>
                        {
                            "Import OpenAPI from your build artifacts.",
                            "Generate deterministic plans for the release tag.",
                            "Fail the pipeline if readiness gates fail."
                        },
                        new CodeSnippet(
                            "GitHub Actions",
                            "Example CI workflow step.",
                            "yaml",
                            "- name: Run ApiTester plans\n  run: |\n    apitester auth token --token $APITESTER_TOKEN\n    apitester openapi import --project api-staging --file openapi.json\n    apitester plans generate --project api-staging\n    apitester plans run --project api-staging --plan release-gate"),
                        Array.Empty<DocCallout>()),
                    new(
                        "outputs",
                        "Capture outputs",
                        "Publish run summaries and evidence links back to the pipeline.",
                        new List<string>
                        {
                            "Export JSON reports for downstream jobs.",
                            "Attach summary links to release notes.",
                            "Fail fast when deterministic plans break."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Tip",
                                "Keep AI probe runs in a separate pipeline step so they never block releases.")
                        })
                }),
            new(
                "environment-config",
                "Environment configuration",
                "Set BaseUrl, lead capture storage, and secrets without committing sensitive data.",
                new List<DocSection>
                {
                    new(
                        "base-url",
                        "Configure ApiTesterWeb BaseUrl",
                        "ApiTester.Site connects to ApiTester.Web for authenticated app views and API calls.",
                        new List<string>
                        {
                            "Set BaseUrl to the HTTPS endpoint for ApiTester.Web.",
                            "Prefer environment variables for production overrides.",
                            "Verify the value matches your reverse proxy host."
                        },
                        new CodeSnippet(
                            "BaseUrl setting",
                            "Point the site to the ApiTester.Web API.",
                            "json",
                            "{\n  \"ApiTesterWeb\": {\n    \"BaseUrl\": \"https://api.example.com\"\n  }\n}"),
                        Array.Empty<DocCallout>()),
                    new(
                        "lead-capture",
                        "Configure lead capture storage",
                        "Lead capture submissions are stored in the LeadCapture database.",
                        new List<string>
                        {
                            "Sqlite is the default when no connection string is provided.",
                            "Use a persistent volume or managed database in production.",
                            "Back up the database alongside other release artifacts."
                        },
                        new CodeSnippet(
                            "Lead capture connection string",
                            "Store lead data in a dedicated database.",
                            "json",
                            "{\n  \"ConnectionStrings\": {\n    \"LeadCapture\": \"Data Source=/var/lib/apitester-site/leads.db\"\n  }\n}"),
                        Array.Empty<DocCallout>()),
                    new(
                        "secrets",
                        "Manage secrets safely",
                        "Keep API keys and credentials out of source control and build artifacts.",
                        new List<string>
                        {
                            "Do not commit appsettings.Production.json with secrets.",
                            "Inject secrets with environment variables or a secret manager.",
                            "Rotate keys regularly and revoke unused credentials."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Security",
                                "ApiTester.Site does not log API keys. Avoid adding custom logging for headers or tokens.")
                        })
                }),
            new(
                "deployment",
                "Deployment guide",
                "Deploy ApiTester.Site with IIS, Kestrel, or containers without exposing core logic.",
                new List<DocSection>
                {
                    new(
                        "iis",
                        "Deploy behind IIS",
                        "Use IIS as a reverse proxy with the ASP.NET Core Hosting Bundle installed.",
                        new List<string>
                        {
                            "Publish with a Release build and copy the output to the IIS site directory.",
                            "Enable HTTPS bindings and forward headers from the proxy.",
                            "Run the app pool with a dedicated identity and least privilege."
                        },
                        new CodeSnippet(
                            "Publish for IIS",
                            "Generate the deployment output for IIS.",
                            "bash",
                            "dotnet publish ApiTester.Site/ApiTester.Site.csproj -c Release -o ./publish"),
                        Array.Empty<DocCallout>()),
                    new(
                        "kestrel",
                        "Run as a Kestrel service",
                        "Kestrel can run as a systemd service behind Nginx or an L7 load balancer.",
                        new List<string>
                        {
                            "Set ASPNETCORE_ENVIRONMENT=Production in the service definition.",
                            "Bind Kestrel to localhost and let the proxy terminate TLS.",
                            "Use a dedicated service account with a locked-down working directory."
                        },
                        new CodeSnippet(
                            "systemd service",
                            "Example systemd unit for ApiTester.Site.",
                            "ini",
                            "[Service]\nWorkingDirectory=/opt/apitester/site\nExecStart=/opt/apitester/site/ApiTester.Site\nEnvironment=ASPNETCORE_ENVIRONMENT=Production\nRestart=always\nUser=apitester"),
                        Array.Empty<DocCallout>()),
                    new(
                        "container",
                        "Container deployment",
                        "Package the site in a container image and run it with minimal runtime dependencies.",
                        new List<string>
                        {
                            "Expose only the HTTP port used by your reverse proxy.",
                            "Mount a volume for LeadCapture storage if using Sqlite.",
                            "Inject secrets via your orchestrator secret store."
                        },
                        new CodeSnippet(
                            "Run container",
                            "Example docker run command.",
                            "bash",
                            "docker run -d \\\n  -e ApiTesterWeb__BaseUrl=https://api.example.com \\\n  -e ConnectionStrings__LeadCapture=Data\\ Source=/data/leads.db \\\n  -v /srv/apitester-site:/data \\\n  -p 8080:8080 apitester/site:latest"),
                        Array.Empty<DocCallout>())
                }),
            new(
                "release-checklist",
                "Release checklist",
                "Verify configuration, secrets, and hosting readiness before shipping ApiTester.Site.",
                new List<DocSection>
                {
                    new(
                        "pre-release",
                        "Pre-release validation",
                        "Confirm configuration and secrets before deploying.",
                        new List<string>
                        {
                            "Confirm ApiTesterWeb BaseUrl points to the correct environment.",
                            "Validate LeadCapture storage backups and retention.",
                            "Ensure no secrets are committed in appsettings.json or appsettings.Production.json."
                        },
                        null,
                        Array.Empty<DocCallout>()),
                    new(
                        "deployment-steps",
                        "Deployment steps",
                        "Deploy the site and validate runtime health.",
                        new List<string>
                        {
                            "Publish the Release build and deploy to the target host.",
                            "Verify TLS certificates, reverse proxy headers, and health endpoints.",
                            "Confirm that API keys are accepted and not written to logs."
                        },
                        null,
                        new List<DocCallout>
                        {
                            new(
                                "Security",
                                "Review log sinks to confirm headers and request bodies are not logged.")
                        }),
                    new(
                        "post-release",
                        "Post-release checks",
                        "Monitor the site after deployment.",
                        new List<string>
                        {
                            "Validate lead submissions are stored successfully.",
                            "Monitor error rates and latency for the first 24 hours.",
                            "Rotate any temporary deployment credentials."
                        },
                        null,
                        Array.Empty<DocCallout>())
                })
        };

        var apiReference = new DocsApiReference(
            "API reference",
            "Curated endpoints for project setup, OpenAPI imports, and run history access.",
            new List<ApiEndpoint>
            {
                new(
                    "import-openapi",
                    "POST",
                    "/api/openapi/import",
                    "Import an OpenAPI document",
                    "Uploads an OpenAPI document from a file or URL and refreshes deterministic plans.",
                    new CodeSnippet(
                        "Request",
                        "Import an OpenAPI contract.",
                        "json",
                        "{\n  \"projectKey\": \"api-staging\",\n  \"sourceUrl\": \"https://api.example.com/openapi.json\"\n}"),
                    new CodeSnippet(
                        "Response",
                        "Import status.",
                        "json",
                        "{\n  \"importId\": \"imp_4012\",\n  \"status\": \"Queued\",\n  \"warnings\": []\n}"),
                    new List<string>
                    {
                        "Returns warnings when schemas or responses are missing.",
                        "Triggers plan generation if auto-generate is enabled."
                    }),
                new(
                    "list-projects",
                    "GET",
                    "/api/projects",
                    "List projects",
                    "Returns available projects for the authenticated user.",
                    new CodeSnippet(
                        "Request",
                        "Fetch projects.",
                        "bash",
                        "curl -H \"Authorization: Bearer $APITESTER_TOKEN\" \\\n  https://apitester.example.com/api/projects"),
                    new CodeSnippet(
                        "Response",
                        "Project list.",
                        "json",
                        "[{\n  \"key\": \"api-staging\",\n  \"name\": \"API Staging\",\n  \"environments\": [\"staging\", \"prod\"]\n}]"),
                    Array.Empty<string>()),
                new(
                    "create-project",
                    "POST",
                    "/api/projects",
                    "Create a project",
                    "Creates a new project and returns its key.",
                    new CodeSnippet(
                        "Request",
                        "Create a new project.",
                        "json",
                        "{\n  \"name\": \"Payments API\",\n  \"ownerTeam\": \"Platform\"\n}"),
                    new CodeSnippet(
                        "Response",
                        "New project details.",
                        "json",
                        "{\n  \"key\": \"payments\",\n  \"name\": \"Payments API\"\n}"),
                    Array.Empty<string>()),
                new(
                    "run-plan",
                    "POST",
                    "/api/projects/{projectId}/test-plans/run",
                    "Run a test plan",
                    "Executes a deterministic plan run for the selected project.",
                    new CodeSnippet(
                        "Request",
                        "Run a deterministic plan.",
                        "json",
                        "{\n  \"planKey\": \"release-gate\",\n  \"environment\": \"staging\"\n}"),
                    new CodeSnippet(
                        "Response",
                        "Run created.",
                        "json",
                        "{\n  \"runId\": \"run_8843\",\n  \"status\": \"Running\"\n}"),
                    new List<string>
                    {
                        "Use query parameters to enable baseline comparisons.",
                        "Run status updates stream via the runs endpoint."
                    }),
                new(
                    "get-run",
                    "GET",
                    "/api/runs/{runId}",
                    "Get run details",
                    "Returns status, assertions, and artifacts for a specific run.",
                    new CodeSnippet(
                        "Request",
                        "Get a run by id.",
                        "bash",
                        "curl -H \"Authorization: Bearer $APITESTER_TOKEN\" \\\n  https://apitester.example.com/api/runs/run_8843"),
                    new CodeSnippet(
                        "Response",
                        "Run details.",
                        "json",
                        "{\n  \"runId\": \"run_8843\",\n  \"status\": \"Passed\",\n  \"startedAt\": \"2026-01-05T10:12:44Z\",\n  \"artifacts\": [\"baseline-diff\", \"trace\"]\n}"),
                    Array.Empty<string>()),
                new(
                    "list-runs",
                    "GET",
                    "/api/runs",
                    "List run history",
                    "Filter runs by project, environment, or status.",
                    new CodeSnippet(
                        "Request",
                        "List runs for a project.",
                        "bash",
                        "curl -H \"Authorization: Bearer $APITESTER_TOKEN\" \\\n  \"https://apitester.example.com/api/runs?projectKey=api-staging&status=Failed\""),
                    new CodeSnippet(
                        "Response",
                        "Run list.",
                        "json",
                        "[{\n  \"runId\": \"run_8831\",\n  \"status\": \"Failed\",\n  \"planKey\": \"release-gate\"\n}]"),
                    Array.Empty<string>())
            });

        var navGroups = new List<DocNavGroup>
        {
            new(
                "Core workflows",
                new List<DocNavItem>
                {
                    new("Getting started", "/docs/getting-started", "Install MCP and run your first plan."),
                    new("Import OpenAPI", "/docs/import-openapi", "Sync contracts and generate deterministic plans."),
                    new("Running test plans", "/docs/running-test-plans", "Execute deterministic suites and AI probes."),
                    new("Viewing run history", "/docs/run-history", "Review baselines, exports, and evidence.")
                }),
            new(
                "Security + operations",
                new List<DocNavItem>
                {
                    new("Policies + SSRF safety", "/docs/policies-ssrf", "Lock outbound traffic to allowlists."),
                    new("Persistence setup", "/docs/persistence", "Choose SqlServer or file storage."),
                    new("CI usage", "/docs/ci-usage", "Run deterministic plans in pipelines.")
                }),
            new(
                "Deployment",
                new List<DocNavItem>
                {
                    new("Environment configuration", "/docs/environment-config", "Set BaseUrl, DB, and secrets."),
                    new("Deployment guide", "/docs/deployment", "Ship the site on IIS, Kestrel, or containers."),
                    new("Release checklist", "/docs/release-checklist", "Confirm readiness before releasing.")
                }),
            new(
                "Reference",
                new List<DocNavItem>
                {
                    new("API reference", "/docs/api-reference", "Curated Web API endpoints with examples.")
                })
        };

        var landing = new DocsLandingContent(
            "Developer docs",
            "Step-by-step guidance for evaluating ApiTester, from MCP setup to CI-ready evidence.",
            new List<string>
            {
                "Follow quickstart guidance and run deterministic plans within minutes.",
                "Keep outbound traffic safe with SSRF guardrails and allowlists.",
                "Export run history with redacted evidence for compliance review."
            },
            navGroups,
            "Need deeper guidance?",
            "Reach out for onboarding sessions, migration help, or security reviews.");

        var searchIndex = BuildSearchIndex(pages, apiReference);

        return new DocsLibrary(landing, pages, apiReference, searchIndex);
    }

    private static IReadOnlyList<DocSearchEntry> BuildSearchIndex(
        IReadOnlyList<DocPage> pages,
        DocsApiReference apiReference)
    {
        var entries = new List<DocSearchEntry>();

        foreach (var page in pages)
        {
            entries.Add(new DocSearchEntry(
                page.Title,
                $"/docs/{page.Slug}",
                page.Summary,
                new[] { page.Slug, "docs" }));

            foreach (var section in page.Sections)
            {
                entries.Add(new DocSearchEntry(
                    section.Heading,
                    $"/docs/{page.Slug}#{section.Id}",
                    section.Body,
                    new[] { page.Title, section.Id, "docs" }));
            }
        }

        entries.Add(new DocSearchEntry(
            apiReference.Title,
            "/docs/api-reference",
            apiReference.Summary,
            new[] { "api", "reference", "endpoints" }));

        foreach (var endpoint in apiReference.Endpoints)
        {
            entries.Add(new DocSearchEntry(
                $"{endpoint.Method} {endpoint.Path}",
                $"/docs/api-reference#{endpoint.Id}",
                endpoint.Summary,
                new[] { endpoint.Method, endpoint.Path, "api" }));
        }

        return entries;
    }
}

public sealed record DocsLibrary(
    DocsLandingContent Landing,
    IReadOnlyList<DocPage> Pages,
    DocsApiReference ApiReference,
    IReadOnlyList<DocSearchEntry> SearchIndex);

public sealed record DocsLandingContent(
    string HeroTitle,
    string HeroSubtitle,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<DocNavGroup> NavGroups,
    string SupportTitle,
    string SupportSubtitle);

public sealed record DocNavGroup(string Title, IReadOnlyList<DocNavItem> Items);

public sealed record DocNavItem(string Title, string Url, string Summary);

public sealed record DocPage(
    string Slug,
    string Title,
    string Summary,
    IReadOnlyList<DocSection> Sections);

public sealed record DocSection(
    string Id,
    string Heading,
    string Body,
    IReadOnlyList<string> Bullets,
    CodeSnippet? CodeSnippet,
    IReadOnlyList<DocCallout> Callouts);

public sealed record DocCallout(string Title, string Body);

public sealed record DocsApiReference(
    string Title,
    string Summary,
    IReadOnlyList<ApiEndpoint> Endpoints);

public sealed record ApiEndpoint(
    string Id,
    string Method,
    string Path,
    string Summary,
    string Description,
    CodeSnippet RequestExample,
    CodeSnippet ResponseExample,
    IReadOnlyList<string> Notes);

public sealed record DocSearchEntry(
    string Title,
    string Url,
    string Summary,
    IReadOnlyList<string> Keywords);
