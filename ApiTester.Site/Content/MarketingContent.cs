namespace ApiTester.Site.Content;

public sealed record MarketingContent(
    MetadataContent Metadata,
    LayoutContent Layout,
    HomePageContent Home,
    PricingPageContent Pricing,
    SecurityComplianceContent SecurityCompliance,
    QaReportingContent QaReporting,
    DeveloperDocsContent DeveloperDocs,
    UseCasesContent UseCases,
    AboutContent About)
{
    public static MarketingContent Current { get; } = Build();

    private static MarketingContent Build()
    {
        var baseUrl = "https://apitester.example.com";

        PageMetadata BuildPageMetadata(
            string title,
            string description,
            string path,
            string ogImage,
            string ogImageAlt)
            => new(
                title,
                description,
                title,
                description,
                $"{baseUrl}{path}",
                ogImage,
                ogImageAlt);

        var navigationItems = new List<NavigationItem>
        {
            new("Home", "/"),
            new("Pricing", "/pricing"),
            new("Security", "/security"),
            new("QA & Reporting", "/qa-reporting"),
            new("Developer Docs", "/docs"),
            new("Use cases", "/use-cases"),
            new("About", "/about"),
            new("Contact", "/contact")
        };

        var footerGroups = new List<FooterGroup>
        {
            new(
                "Platform",
                new List<NavigationItem>
                {
                    new("Pricing", "/pricing"),
                    new("Security", "/security"),
                    new("QA & Reporting", "/qa-reporting"),
                    new("Developer Docs", "/docs")
                }),
            new(
                "Company",
                new List<NavigationItem>
                {
                    new("Use cases", "/use-cases"),
                    new("About", "/about"),
                    new("Status", "/status")
                })
        };

        var homePersonaTiles = new List<PersonaTile>
        {
            new(
                "Developer",
                "Ship with confidence by catching contract breaks before merge and keeping API docs grounded in evidence.",
                "Import OpenAPI, generate deterministic plans, and let AI propose edge cases while you decide what becomes permanent coverage."),
            new(
                "QA",
                "Turn regression into reusable evidence with run history, baselines, and exportable release packs.",
                "Run deterministic suites, then add AI probes for boundaries, nullability, and failure modes to find the gaps."),
            new(
                "Compliance",
                "Collect audit-ready artefacts without widening access or letting tests drift outside policy.",
                "SSRF guard, allowlists, redaction, and immutable run history give you traceability without overexposure."),
            new(
                "Vibe coder",
                "Prototype fast with AI, then lock in the best flows with deterministic tests and proof you can share.",
                "Ask the AI for coverage ideas, promote the winners, and keep the rest as exploratory probes.")
        };

        var homeWorkflowSteps = new List<WorkflowStep>
        {
            new(
                "Connect your API",
                "Import OpenAPI, map environments, and set allowlists. The SSRF guard blocks localhost and private ranges by default."),
            new(
                "Run deterministic plans",
                "Generate deterministic plans from the spec, execute via MCP, and capture run history with evidence."),
            new(
                "Expand coverage",
                "AI proposes edge cases and negative tests. You choose what becomes repeatable coverage."),
            new(
                "Share evidence",
                "Explain endpoints and failures with grounded AI, then export reports for release and compliance sign-off.")
        };

        var pricingPlans = new List<PricingPlan>
        {
            new(
                "Free",
                "For solo developers or vibe coders validating a new API before sharing results.",
                "£0",
                "per month",
                "Start free",
                "/signup",
                false,
                string.Empty,
                new List<string>
                {
                    "1 project with basic allowlists",
                    "50 deterministic runs per month",
                    "7-day run history retention",
                    "AI-assisted endpoint summaries from OpenAPI evidence",
                    "Community support"
                }),
            new(
                "Pro",
                "For delivery teams that need repeatable evidence every sprint and AI coverage that stays grounded.",
                "£79",
                "per user/month",
                "Upgrade",
                "/upgrade",
                true,
                "Most used",
                new List<string>
                {
                    "Unlimited projects and environments",
                    "Scheduled runs and CI readiness checks",
                    "AI edge case suggestions with promotion workflow",
                    "Run baselines, flaky annotations, and rerun controls",
                    "Audit logs and exportable evidence packs"
                }),
            new(
                "Team",
                "For regulated organisations that need governance, retention controls, and AI evaluation reporting.",
                "Custom",
                "",
                "Talk to sales",
                "/contact",
                false,
                string.Empty,
                new List<string>
                {
                    "Retention and redaction policies per project",
                    "Organisation-wide audit trails and approvals",
                    "SSRF guard configuration and allowlist administration",
                    "AI evaluation scorecards and drift tracking",
                    "Priority support and onboarding"
                })
        };

        var pricingFeatureMatrix = new List<FeatureComparison>
        {
            new("Deterministic run history", new List<string> { "50 runs", "Unlimited", "Unlimited" }),
            new("Retention controls", new List<string> { "7 days", "30 days", "Custom" }),
            new("Policy allowlists", new List<string> { "Basic", "Advanced", "Advanced" }),
            new("SSRF guard controls", new List<string> { "Standard", "Standard", "Custom" }),
            new("Baseline comparison", new List<string> { "Limited", "Included", "Included" }),
            new("AI edge case suggestions", new List<string> { "Limited", "Included", "Included" }),
            new("AI eval scorecards", new List<string> { "Not included", "Optional", "Included" }),
            new("Audit log exports", new List<string> { "Not included", "Included", "Included" })
        };

        var pricingUseCases = new List<UseCaseSummary>
        {
            new("Developer onboarding", "Import the spec, run a plan, and ship an API explainer grounded in evidence."),
            new("QA regression", "Repeatable runs with deterministic plans, plus AI probes for boundary cases."),
            new("Compliance evidence", "Export audit logs, run history, and redacted evidence without overexposing secrets."),
            new("CI release gates", "Trigger suites in pipelines, publish readiness outcomes, and attach summaries to releases.")
        };

        var pricingFaqs = new List<FaqDefinition>
        {
            new("Can we change plans later?", "Yes. You can upgrade or downgrade at any time with pro-rated billing."),
            new("What counts as a run?", "A run is one execution of a deterministic test plan, including retries configured in that plan."),
            new("Do you retain payloads forever?", "No. Retention is configurable per project and can be shortened by policy."),
            new("Will the AI invent API behaviour?", "No. The AI is grounded in your OpenAPI and run evidence. If it lacks proof, it says what is missing.")
        };

        var securityControls = new List<SecurityControl>
        {
            new("SSRF guard", "Outbound traffic is restricted to approved destinations and blocked from private, loopback, and metadata ranges."),
            new("Policy allowlists", "Allowlists define where tests can run, which methods are allowed, and which headers may be used."),
            new("Redaction", "Sensitive headers and payload fields can be masked before storage or export based on policy."),
            new("Audit trail", "Every change, run, and approval is captured with actor, timestamp, and project context."),
            new("Project separation", "Projects isolate credentials, environments, and run history by default."),
            new("Grounded AI", "AI outputs are constrained to your evidence, OpenAPI specs, and run artefacts, with explicit unknowns.")
        };

        var retentionNotes = new List<string>
        {
            "Retention windows are configurable per project and can be shortened or extended by policy.",
            "Evidence exports include run history, approvals, and configuration snapshots when available.",
            "Redaction policies can be applied to stored payloads and export bundles.",
            "We do not claim certifications we do not hold; request the latest assurance pack for current status."
        };

        var qaHighlights = new List<QaHighlight>
        {
            new("Run history", "Track every run with timestamps, owners, and environment markers for audit-ready traceability."),
            new("Baselines", "Compare new runs against a baseline to flag contract drift and regression risk."),
            new("Flaky handling", "Mark flakiness, capture notes, and rerun without rewriting the plan."),
            new("Evidence packs", "Export CSV and JSON summaries for QA sign-off and compliance archives."),
            new("AI exploratory coverage", "AI proposes edge cases and negative tests; you promote the valuable ones into deterministic coverage."),
            new("Failure explanations", "AI summarises why a run failed and which contract broke, while citing evidence.")
        };

        var qaWorkflowSteps = new List<WorkflowStep>
        {
            new("Create deterministic plans", "Define fixtures, preconditions, and assertions once per project."),
            new("Run in CI", "Trigger plans from your pipeline and capture readiness in the run history."),
            new("Add AI probes", "Generate boundary cases and negative tests from the spec, promote the valuable ones."),
            new("Report", "Share run history, baselines, and evidence packs with stakeholders.")
        };

        var docsQuickstartSteps = new List<string>
        {
            "Create a project, set policy allowlists for target environments, and confirm SSRF guard defaults.",
            "Import your OpenAPI spec and map base URLs, tokens, and headers via the runtime config tools.",
            "Generate a deterministic test plan, run it locally or in CI via MCP, and review run history.",
            "Ask grounded questions about endpoints, parameters, and failures, then export evidence reports."
        };

        var docsMcpHighlights = new List<string>
        {
            "MCP exposes the platform as tools, so CLIs, agents, and CI can drive the same workflows as the UI.",
            "SSRF guard rules are enforced before outbound requests are made.",
            "Run results stream back as structured run history with audit logs.",
            "AI tools can be orchestrated safely because tool access is explicit and outputs are grounded in evidence."
        };

        var docsEnvironmentSteps = new List<string>
        {
            "Create environments for dev, staging, and production with scoped credentials.",
            "Assign allowlists per environment so tests can only hit approved hosts and methods.",
            "Apply redaction rules to mask tokens, PII, and secrets before storage or export.",
            "Store environment config snapshots with each run for audit reproducibility."
        };

        var docsCiSteps = new List<string>
        {
            "Generate a token scoped to the project and environment.",
            "Run the MCP client in your pipeline to execute deterministic test plans.",
            "Publish CI readiness status and export artefacts for release notes.",
            "Optionally run eval scorecards to track AI quality and regression over time."
        };

        var docsCodeSnippets = new List<CodeSnippet>
        {
            new(
                "Install and authenticate",
                "Use the MCP client to authenticate and list projects.",
                "bash",
                "dotnet tool install --global apitester.mcp\napitester-mcp auth login --token $APITESTER_TOKEN\napitester-mcp projects list"),
            new(
                "Environment configuration",
                "Define a runtime environment with allowlists and redaction rules.",
                "json",
                "{\n  \"environment\": \"staging\",\n  \"baseUrl\": \"https://api.staging.example.com\",\n  \"allowlist\": [\"api.staging.example.com\"],\n  \"redaction\": {\n    \"headers\": [\"Authorization\"],\n    \"jsonPaths\": [\"$.customer.ssn\"]\n  }\n}"),
            new(
                "CI run",
                "Example GitHub Actions step to run deterministic plans.",
                "yaml",
                "- name: Run ApiTester plan\n  run: |\n    apitester-mcp run plan --project api-platform --environment staging\n  env:\n    APITESTER_TOKEN: ${{ secrets.APITESTER_TOKEN }}")
        };

        var useCaseStories = new List<CaseStudy>
        {
            new(
                "Developer lead",
                "A platform engineer needed quick feedback when APIs changed. ApiTester kept specs and tests aligned, then surfaced CI readiness before merging. The team relied on run history to prove what was tested, plus AI summaries to explain breaking changes to product stakeholders."),
            new(
                "QA lead",
                "A QA team replaced manual spreadsheets with deterministic test plans and repeatable runs. AI suggested boundary and negative cases that were consistently missed, then the team promoted the best ones into deterministic coverage. Exports provided evidence for weekly release sign-off."),
            new(
                "Compliance reviewer",
                "A compliance reviewer required project separation, allowlists, retention controls, and an audit trail. ApiTester provided a policy story, redaction controls, and grounded AI explanations that never invented behaviour outside the spec.")
        };

        var aboutPrinciples = new List<string>
        {
            "Deliver evidence before opinions. Deterministic plans and run history keep teams aligned.",
            "Keep controls visible: SSRF guard, allowlists, redaction, and audit trails are built in.",
            "Make AI useful but bounded. Coverage ideas are promoted with human approval.",
            "Ship faster without losing governance. CI readiness and exports make releases auditable."
        };

        var aboutAudience = new List<string>
        {
            "Developers who want fast feedback without slowing delivery.",
            "QA teams who need repeatable evidence, baselines, and exportable release packs.",
            "Compliance and security teams who need policy-backed controls and audit logs.",
            "Vibe coders who want AI speed with deterministic guardrails."
        };

        var aboutNotList = new List<string>
        {
            "ApiTester is not a production traffic proxy or API gateway.",
            "It does not replace a security testing programme; it enforces safe execution and evidence capture.",
            "It does not execute arbitrary outbound calls outside policy allowlists.",
            "It is not a ticketing or incident management system.",
            "The AI does not make up behaviour; it stays grounded in evidence."
        };

        return new MarketingContent(
            new MetadataContent(
                "ApiTester",
                "AI-first API testing with MCP, deterministic plans, edge case discovery, and grounded documentation.",
                "API testing, MCP, AI testing, edge cases, RAG, OpenAPI, audit logs, CI readiness, SSRF guard, policy allowlists",
                baseUrl,
                "ApiTester, grounded API testing with evidence",
                "Run deterministic API tests via MCP, use AI to propose edge cases, and generate grounded API documentation with audit-grade evidence.",
                "summary_large_image",
                $"{baseUrl}/og/site-placeholder.png",
                "Placeholder illustration for ApiTester marketing"),
            new LayoutContent(
                "ApiTester",
                "AI-first API testing with deterministic plans, run history, and audit-grade evidence.",
                navigationItems,
                footerGroups,
                "Email: support@apitester.example.com",
                "Phone: +44 (0)20 7946 0123",
                "Location: 21 King Street, London, UK",
                "© 2024 ApiTester. All rights reserved."),
            new HomePageContent(
                BuildPageMetadata(
                    "ApiTester | Grounded API testing for dev, QA, compliance, and vibe coders",
                    "Turn OpenAPI specs into deterministic tests, then use AI to expand coverage with evidence you can share.",
                    "/",
                    $"{baseUrl}/og/home-placeholder.png",
                    "Placeholder hero for ApiTester home"),
                "Evidence-first API testing for dev, QA, compliance, and vibe coders.",
                "ApiTester turns your OpenAPI into deterministic test plans, then uses AI to propose edge cases and produce grounded API documentation via MCP, with run history, baselines, and audit-grade evidence you can share.",
                "Book a walkthrough",
                "/contact",
                "Read developer docs",
                "/docs",
                new VisualPlaceholder(
                    "Hero visual placeholder",
                    "Planned visual of deterministic plans, AI edge case discovery, and grounded API explanations."),
                "Outcomes for every role",
                homePersonaTiles,
                "Workflow",
                "A predictable path from spec to tests to evidence, with AI doing the tedious parts and CI readiness built in.",
                homeWorkflowSteps,
                new VisualPlaceholder(
                    "Workflow animation placeholder",
                    "Planned visual showing import, test execution, AI analysis, and evidence export."),
                "Proof points",
                new List<ProofPoint>
                {
                    new("Import OpenAPI", "Load your spec once and keep it as the source of truth for tests and documentation."),
                    new("Deterministic plans", "Generate deterministic test plans from your OpenAPI, run them, and store evidence."),
                    new("AI edge case discovery", "AI proposes boundaries, nulls, weird payloads, and negative scenarios, then you promote what matters."),
                    new("Grounded API documentation", "AI explains endpoints, parameters, auth, and error contracts using only your spec and run evidence."),
                    new("Policy-first execution", "Allowlist base URLs and methods, block localhost and private networks, and enforce SSRF guard."),
                    new("Run history", "Filter run history by operationId, export CSV, and share reports."),
                    new("Audit trail", "Track who ran what, when, and what changed for compliance reviews."),
                    new("Built for CI", "Headless by default via MCP, no UI clicking required, consistent results.")
                },
                "Move from testing to evidence in one workflow.",
                "Create deterministic test plans, run them under policy, add AI coverage for edge cases, and share audit-ready proof.",
                "Request a demo",
                "/contact"),
            new PricingPageContent(
                BuildPageMetadata(
                    "ApiTester pricing | Plans for developers, QA, and compliance",
                    "Compare Free, Pro, and Team plans with deterministic runs, AI coverage, and governance controls.",
                    "/pricing",
                    $"{baseUrl}/og/pricing-placeholder.png",
                    "Placeholder pricing plan graphic"),
                "Pricing that supports delivery and governance",
                "Choose a plan that matches how you ship: individual validation, team QA, or compliance-led rollouts with AI evaluation and cost controls.",
                "Start free",
                "/signup",
                "Talk to sales",
                "/contact",
                pricingPlans,
                "Included across all plans",
                new List<string>
                {
                    "Deterministic test plans with versioned changes",
                    "Run history with exportable evidence",
                    "Project separation and scoped access",
                    "SSRF guard defaults and policy allowlists",
                    "Grounded AI summaries of your API from specs and evidence"
                },
                new VisualPlaceholder(
                    "Pricing visual placeholder",
                    "Planned comparison visual for plan coverage, AI capabilities, and evidence."),
                "Compare plans",
                "Plan features aligned to delivery, governance, and AI quality control.",
                "Capability",
                "Ask about governance",
                "/contact",
                pricingFeatureMatrix,
                "Teams using ApiTester",
                "Short examples of how teams adopt consistent API evidence and grounded AI documentation.",
                pricingUseCases,
                "Pricing FAQs",
                "Clear answers to common buying questions.",
                pricingFaqs),
            new SecurityComplianceContent(
                BuildPageMetadata(
                    "ApiTester security | SSRF guard, allowlists, retention, and audit trails",
                    "Run API tests safely with SSRF guard, allowlists, redaction, retention controls, and audit-ready evidence.",
                    "/security",
                    $"{baseUrl}/og/security-placeholder.png",
                    "Placeholder security controls graphic"),
                "Security & compliance",
                "Controls are built in so teams can run tests and AI analysis without broad access or hidden risk.",
                "Security controls",
                securityControls,
                "Retention and evidence",
                retentionNotes,
                new VisualPlaceholder(
                    "Security controls placeholder",
                    "Planned visual for SSRF guard, allowlist policy flows, and evidence exports."),
                "Need a security review?",
                "We can share our latest control summary, data handling notes, AI grounding approach, and audit log samples.",
                "Contact security",
                "/contact"),
            new QaReportingContent(
                BuildPageMetadata(
                    "ApiTester QA & reporting | Run history, baselines, and evidence",
                    "Track run history, baselines, flakes, and exports so QA can ship with confidence.",
                    "/qa-reporting",
                    $"{baseUrl}/og/qa-placeholder.png",
                    "Placeholder QA reporting graphic"),
                "QA & reporting",
                "Repeatable API evidence with deterministic test plans, run history, AI edge case discovery, and exportable reports.",
                "QA outcomes",
                qaHighlights,
                "Workflow",
                "A predictable path from plan to evidence, plus AI coverage for the messy bits.",
                qaWorkflowSteps,
                new VisualPlaceholder(
                    "QA workflow placeholder",
                    "Planned visual for run history, evidence exports, and AI assisted analysis."),
                "Turn QA work into evidence, not noise.",
                "Capture run history, explain flakiness, compare baselines, and export proof, plus use AI to expand coverage safely.",
                "Discuss QA workflows",
                "/contact"),
            new DeveloperDocsContent(
                BuildPageMetadata(
                    "ApiTester developer docs | MCP quickstart and CI workflow",
                    "Quickstart ApiTester MCP, configure environments, and add CI-ready run steps.",
                    "/docs",
                    $"{baseUrl}/og/docs-placeholder.png",
                    "Placeholder developer docs graphic"),
                "Developer docs",
                "Guidance for teams evaluating the MCP workflow, AI tools, and CI usage.",
                "Quickstart",
                docsQuickstartSteps,
                "MCP client overview",
                docsMcpHighlights,
                "Environment configuration",
                docsEnvironmentSteps,
                "CI usage",
                docsCiSteps,
                docsCodeSnippets,
                new VisualPlaceholder(
                    "Developer workflow placeholder",
                    "Planned visual of MCP tool calls, CI runs, and AI grounded outputs."),
                "Start with MCP",
                "Set policy allowlists, run deterministic plans, ask grounded AI questions, and export evidence in minutes.",
                "Download the MCP client",
                "/contact"),
            new UseCasesContent(
                BuildPageMetadata(
                    "ApiTester use cases | Dev, QA, and compliance stories",
                    "See how developers, QA leads, and compliance reviewers use ApiTester for evidence and AI coverage.",
                    "/use-cases",
                    $"{baseUrl}/og/use-cases-placeholder.png",
                    "Placeholder use case graphic"),
                "Use cases",
                "Short stories from teams who need evidence they can trust, and AI explanations they can defend.",
                useCaseStories,
                "See your use case in a real workflow.",
                "We can share examples tied to deterministic test plans, run history, AI edge case discovery, and audit logs.",
                "Request examples",
                "/contact"),
            new AboutContent(
                BuildPageMetadata(
                    "About ApiTester | Product philosophy and positioning",
                    "Learn who ApiTester is for, what we believe, and what we intentionally are not.",
                    "/about",
                    $"{baseUrl}/og/about-placeholder.png",
                    "Placeholder about graphic"),
                "About ApiTester",
                "We build for teams who need practical evidence, not noise, and AI that stays inside guard rails.",
                "Product philosophy",
                aboutPrinciples,
                "Who it’s for",
                aboutAudience,
                "What it is not",
                aboutNotList,
                "Work with a product that stays within policy.",
                "We help teams show evidence, not shortcuts, and we are clear about the limits, including what AI can and cannot infer.",
                "Speak with the team",
                "/contact"));
    }
}

public sealed record MetadataContent(
    string Title,
    string Description,
    string Keywords,
    string CanonicalUrl,
    string OgTitle,
    string OgDescription,
    string TwitterCard,
    string OgImage,
    string OgImageAlt);

public sealed record PageMetadata(
    string Title,
    string Description,
    string OgTitle,
    string OgDescription,
    string CanonicalUrl,
    string OgImage,
    string OgImageAlt);

public sealed record LayoutContent(
    string BrandName,
    string FooterSummary,
    IReadOnlyList<NavigationItem> Navigation,
    IReadOnlyList<FooterGroup> FooterGroups,
    string ContactEmail,
    string ContactPhone,
    string ContactLocation,
    string Copyright);

public sealed record NavigationItem(string Label, string Url);

public sealed record FooterGroup(string Title, IReadOnlyList<NavigationItem> Links);

public sealed record HomePageContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    string PrimaryCtaLabel,
    string PrimaryCtaLink,
    string SecondaryCtaLabel,
    string SecondaryCtaLink,
    VisualPlaceholder HeroPlaceholder,
    string PersonaTitle,
    IReadOnlyList<PersonaTile> PersonaTiles,
    string WorkflowTitle,
    string WorkflowSubtitle,
    IReadOnlyList<WorkflowStep> WorkflowSteps,
    VisualPlaceholder WorkflowPlaceholder,
    string ProofTitle,
    IReadOnlyList<ProofPoint> ProofPoints,
    string ClosingCtaTitle,
    string ClosingCtaSubtitle,
    string ClosingCtaLabel,
    string ClosingCtaLink);

public sealed record PersonaTile(string Title, string Outcome, string Proof);

public sealed record WorkflowStep(string Title, string Description);

public sealed record VisualPlaceholder(string Title, string Description);

public sealed record ProofPoint(string Title, string Description);

public sealed record PricingPageContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    string PrimaryCtaLabel,
    string PrimaryCtaLink,
    string SecondaryCtaLabel,
    string SecondaryCtaLink,
    IReadOnlyList<PricingPlan> Plans,
    string ValuePropsTitle,
    IReadOnlyList<string> ValueProps,
    VisualPlaceholder ValuePropsPlaceholder,
    string ComparisonTitle,
    string ComparisonSubtitle,
    string ComparisonHeader,
    string ComparisonCtaLabel,
    string ComparisonCtaLink,
    IReadOnlyList<FeatureComparison> FeatureMatrix,
    string UseCasesTitle,
    string UseCasesSubtitle,
    IReadOnlyList<UseCaseSummary> UseCases,
    string FaqTitle,
    string FaqSubtitle,
    IReadOnlyList<FaqDefinition> Faqs);

public sealed record PricingPlan(
    string Name,
    string Tagline,
    string Price,
    string PriceSuffix,
    string CtaLabel,
    string CtaLink,
    bool IsFeatured,
    string Badge,
    IReadOnlyList<string> Highlights);

public sealed record FeatureComparison(string Feature, IReadOnlyList<string> Values);

public sealed record UseCaseSummary(string Title, string Description);

public sealed record FaqDefinition(string Question, string Answer);

public sealed record SecurityComplianceContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    string ControlsTitle,
    IReadOnlyList<SecurityControl> Controls,
    string RetentionTitle,
    IReadOnlyList<string> RetentionNotes,
    VisualPlaceholder ControlsPlaceholder,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);

public sealed record SecurityControl(string Title, string Description);

public sealed record QaReportingContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    string HighlightsTitle,
    IReadOnlyList<QaHighlight> Highlights,
    string WorkflowTitle,
    string WorkflowSubtitle,
    IReadOnlyList<WorkflowStep> WorkflowSteps,
    VisualPlaceholder WorkflowPlaceholder,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);

public sealed record QaHighlight(string Title, string Description);

public sealed record DeveloperDocsContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    string QuickstartTitle,
    IReadOnlyList<string> QuickstartSteps,
    string McpTitle,
    IReadOnlyList<string> McpHighlights,
    string EnvironmentTitle,
    IReadOnlyList<string> EnvironmentSteps,
    string CiTitle,
    IReadOnlyList<string> CiSteps,
    IReadOnlyList<CodeSnippet> CodeSnippets,
    VisualPlaceholder WorkflowPlaceholder,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);

public sealed record CodeSnippet(
    string Title,
    string Description,
    string Language,
    string Code);

public sealed record UseCasesContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    IReadOnlyList<CaseStudy> CaseStudies,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);

public sealed record CaseStudy(string Title, string Story);

public sealed record AboutContent(
    PageMetadata Metadata,
    string HeroTitle,
    string HeroSubtitle,
    string PhilosophyTitle,
    IReadOnlyList<string> PhilosophyPoints,
    string AudienceTitle,
    IReadOnlyList<string> AudiencePoints,
    string NotTitle,
    IReadOnlyList<string> NotPoints,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);
