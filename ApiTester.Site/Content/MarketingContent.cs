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
        var navigationItems = new List<NavigationItem>
        {
            new("Home", "/"),
            new("Pricing", "/pricing"),
            new("Security & Compliance", "/security"),
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
                    new("Security & Compliance", "/security"),
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
                "Ship with confidence, catch breaking changes early, and stop hand testing endpoints.",
                "Import OpenAPI, generate a deterministic plan, then let AI propose edge cases you would not think to write at 5pm.")
            ,
            new(
                "QA",
                "Turn regression into evidence, not spreadsheets, plus AI assisted exploratory coverage for the unknown unknowns.",
                "Run deterministic suites, then add AI probes for boundaries, nullability, weird payloads, and error contracts.")
            ,
            new(
                "Compliance",
                "Collect audit ready evidence while keeping access controlled and traffic inside policy.",
                "MCP tooling, allowlists, SSRF guard, and immutable run history give you traceability without widening secrets access.")
        };

        var homeWorkflowSteps = new List<WorkflowStep>
        {
            new(
                "Connect your API",
                "Import OpenAPI, map environments, and set runtime policy allowlists, SSRF guard blocks localhost and private networks by default."),
            new(
                "Generate and run tests",
                "Create deterministic plans from the spec, run them headless via MCP, then let AI suggest edge cases and negative tests."),
            new(
                "Explain and evidence",
                "AI describes endpoints, parameters, and behaviour using only your spec and run evidence, with citations, then exports reports for stakeholders.")
        };

        var pricingPlans = new List<PricingPlan>
        {
            new(
                "Free",
                "For developers validating a new API, importing a spec, running a plan, and getting basic AI assisted documentation.",
                "£0",
                "per month",
                "Start free",
                "/signup",
                false,
                string.Empty,
                new List<string>
                {
                    "1 project with project separation controls",
                    "Manual runs with basic run history",
                    "Deterministic test plan templates",
                    "AI assisted endpoint summaries from OpenAPI evidence",
                    "Community support"
                }),
            new(
                "Pro",
                "For teams that need repeatable evidence every sprint, plus AI driven edge case coverage and API explainers that stay grounded in your specs.",
                "£79",
                "per user/month",
                "Upgrade",
                "/upgrade",
                true,
                "Most used",
                new List<string>
                {
                    "Unlimited projects with policy allowlists",
                    "Scheduled runs and CI readiness checks",
                    "AI generated edge case suggestions, negative tests, and boundary probes",
                    "RAG grounded API descriptions, parameters, and example requests",
                    "Flaky handling notes and rerun controls",
                    "Audit logs and exports for stakeholders"
                }),
            new(
                "Team",
                "For regulated organisations requiring governance, environments, and cost control, plus evaluation reports to track AI quality over time.",
                "Custom",
                "",
                "Talk to sales",
                "/contact",
                false,
                string.Empty,
                new List<string>
                {
                    "Dedicated retention settings and evidence exports",
                    "SSRF guard configuration and allowlist administration",
                    "Organisation-wide audit logs",
                    "AI evaluation reports, scorecards, drift tracking, and approval workflows",
                    "Priority support and onboarding"
                })
        };

        var pricingFeatureMatrix = new List<FeatureComparison>
        {
            new("Run history and evidence exports", new List<string> { "Limited", "Included", "Included" }),
            new("Deterministic test plans", new List<string> { "Included", "Included", "Included" }),
            new("Policy allowlists", new List<string> { "Basic", "Advanced", "Advanced" }),
            new("SSRF guard controls", new List<string> { "Standard", "Standard", "Custom" }),
            new("CI readiness checks", new List<string> { "Manual", "Automated", "Automated" }),
            new("AI edge case suggestions", new List<string> { "Limited", "Included", "Included" }),
            new("RAG grounded API explainer", new List<string> { "Basic", "Included", "Included" }),
            new("AI eval scorecards and drift", new List<string> { "Not included", "Optional", "Included" })
        };

        var pricingUseCases = new List<UseCaseSummary>
        {
            new("Developer onboarding", "Import the spec, run a plan, and get an AI generated, grounded endpoint guide in minutes."),
            new("QA regression", "Repeatable runs with deterministic plans, plus AI probes for boundary cases and negative scenarios."),
            new("Compliance evidence", "Export audit logs, run history, and AI evaluation scorecards without exposing production secrets."),
            new("CI coverage", "Trigger suites in pipelines, publish readiness outcomes, and attach AI explanations to release notes.")
        };

        var pricingFaqs = new List<FaqDefinition>
        {
            new("Can we change plans later?", "Yes. You can upgrade or downgrade at any time with pro-rated billing."),
            new("What does a run include?", "A run is a complete execution of a deterministic test plan, including retries within the same run."),
            new("Do you store payloads forever?", "No. Retention is configurable and can be set per project."),
            new("Is the AI allowed to invent API behaviour?", "No. The AI is grounded in your OpenAPI and run evidence. If it cannot prove something, it says what evidence is missing.")
        };

        var securityControls = new List<SecurityControl>
        {
            new("SSRF guard", "Outbound traffic is restricted to approved destinations and blocked from private, loopback, and metadata ranges."),
            new("Policy allowlists", "Allowlist policies define where tests can run, which methods are allowed, and which headers may be used."),
            new("Audit logs", "Every change, run, and approval is captured with actor, timestamp, and project context."),
            new("Project separation", "Projects isolate credentials, environments, and run history by default."),
            new("Grounded AI", "AI outputs are constrained to your evidence, OpenAPI specs and run artefacts, with citations and explicit unknowns."),
            new("Prompt injection resilience", "RAG prompts enforce evidence-only answers, tool access is explicit, and inputs are treated as untrusted.")
        };

        var retentionNotes = new List<string>
        {
            "Retention windows are configurable per project and can be shortened or extended by policy.",
            "Evidence exports include run history, approvals, and configuration snapshots when available.",
            "AI evaluation reports can be persisted as artefacts and diffed over time to track quality drift.",
            "We do not claim certifications we do not hold, ask for the latest assurance pack."
        };

        var qaHighlights = new List<QaHighlight>
        {
            new("Run history", "Track every run with timestamps, owners, and environment markers for audit-ready traceability."),
            new("Repeatability", "Deterministic test plans ensure the same inputs and assertions can be rerun on demand."),
            new("AI exploratory coverage", "AI proposes edge cases, negative tests, and boundary probes based on your spec and failures, then you choose what becomes deterministic."),
            new("Flaky handling", "Mark flakiness, capture notes, and rerun without rewriting the plan. Automated retries are configurable."),
            new("Exports and evidence", "Export CSV and JSON summaries for QA sign-off and compliance archives."),
            new("Explain failures", "AI summarises why a run failed, what endpoint and contract broke, and what changed, while citing the evidence.")
        };

        var qaWorkflowSteps = new List<WorkflowStep>
        {
            new("Create deterministic plans", "Define fixtures, preconditions, and assertions once per project."),
            new("Run in CI", "Trigger plans from your pipeline and capture CI readiness in the run history."),
            new("Add AI probes", "Generate boundary cases and negative tests from the spec, promote the valuable ones into deterministic coverage."),
            new("Share evidence", "Attach audit logs, exports, and AI eval scorecards to release sign-off packs.")
        };

        var docsQuickstartSteps = new List<string>
        {
            "Create a project, set policy allowlists for target environments, and confirm SSRF guard defaults.",
            "Import your OpenAPI spec and map base URLs, tokens, and headers via the runtime config tools.",
            "Generate a deterministic test plan, run it locally or in CI via MCP, and review run history.",
            "Index the spec for RAG, ask grounded questions about endpoints and parameters, and export evidence reports."
        };

        var docsMcpHighlights = new List<string>
        {
            "MCP exposes the platform as tools, so CLIs, agents, and CI can drive the same workflows as the UI.",
            "SSRF guard rules are enforced before outbound requests are made.",
            "Run results stream back as structured run history with audit logs.",
            "AI tools can be orchestrated safely because tool access is explicit and outputs are grounded in evidence."
        };

        var docsCiSteps = new List<string>
        {
            "Generate a token scoped to the project and environment.",
            "Run the MCP client in your pipeline to execute deterministic test plans.",
            "Publish CI readiness status and export artefacts for release notes.",
            "Optionally run eval scorecards to track AI quality and regression over time."
        };

        var useCaseStories = new List<CaseStudy>
        {
            new(
                "Dev lead",
                "A platform engineer needed quick feedback when APIs changed. ApiTester kept specs and tests aligned, then surfaced CI readiness before merging. The team relied on run history to prove what was tested, plus AI summaries to explain breaking changes to non engineers."),
            new(
                "QA lead",
                "A QA team replaced manual spreadsheets with deterministic test plans and repeatable runs. AI suggested boundary and negative cases that were consistently missed, then the team promoted the best ones into deterministic coverage. Exports provided evidence for weekly release sign-off."),
            new(
                "Security reviewer",
                "A security reviewer required project separation, allowlists, and SSRF guard. ApiTester provided an execution policy story, audit logs, and evidence packs, plus grounded AI explanations that never invented behaviour outside the spec." )
        };

        var aboutPrinciples = new List<string>
        {
            "Deliver outcomes before features, evidence, confidence, and controlled access.",
            "Keep controls visible, SSRF guard, policy allowlists, audit logs, and project separation are not optional add-ons.",
            "Stay practical, deterministic test plans and CI readiness reduce debate during releases.",
            "Use AI where it actually helps, for coverage gaps, edge cases, and clear API explanations, grounded in evidence."
        };

        var aboutAudience = new List<string>
        {
            "Developers who want clear feedback without slowing delivery.",
            "QA teams who need repeatable evidence with run history, plus AI assisted exploratory coverage.",
            "Compliance and security teams who need policy-backed controls and audit logs.",
            "Fast-moving teams who want a safe path to shipping, with AI doing the grunt work and humans approving what matters."
        };

        var aboutNotList = new List<string>
        {
            "ApiTester is not a production traffic proxy or API gateway.",
            "It does not replace a proper security testing programme, it enforces safe execution and evidence capture for API tests.",
            "It does not execute arbitrary outbound calls outside policy allowlists.",
            "It is not a general-purpose ticketing or incident management tool.",
            "The AI does not make up behaviour, it is constrained to evidence and will say what it cannot prove."
        };

        return new MarketingContent(
            new MetadataContent(
                "ApiTester",
                "AI first API testing with MCP, deterministic plans, edge case discovery, and grounded API documentation.",
                "API testing, MCP, AI testing, edge cases, RAG, OpenAPI, audit logs, CI readiness, SSRF guard, policy allowlists",
                "https://apitester.example.com",
                "ApiTester, AI first API testing with evidence",
                "Run deterministic API tests via MCP, use AI to propose edge cases, and generate grounded API documentation with audit-grade evidence.",
                "summary_large_image",
                "/images/hero-illustration.svg",
                "Illustration of the ApiTester testing workspace"),
            new LayoutContent(
                "ApiTester",
                "AI first API testing with deterministic plans, run history, and audit-grade evidence.",
                navigationItems,
                footerGroups,
                "Email: support@apitester.example.com",
                "Phone: +44 (0)20 7946 0123",
                "Location: 21 King Street, London, UK",
                "© 2024 ApiTester. All rights reserved."),
            new HomePageContent(
                "AI first API testing, deterministic when it matters, creative where it helps.",
                "ApiTester turns your OpenAPI into deterministic test plans, then uses AI to propose edge cases and produce grounded API documentation via MCP, with run history and audit-grade evidence.",
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
                    new("Import OpenAPI", "Load your spec once, keep it as the source of truth for tests and documentation."),
                    new("Deterministic plans", "Generate deterministic test plans from your OpenAPI, run them, store evidence."),
                    new("AI edge case discovery", "AI proposes boundaries, nulls, weird payloads, negative scenarios, and you promote the valuable ones into deterministic coverage."),
                    new("Grounded API documentation", "AI explains endpoints, parameters, auth, and error contracts using only your spec and run evidence, with citations."),
                    new("Policy-first execution", "Allowlist base URLs and methods, block localhost, block private networks, and enforce SSRF guard before any request."),
                    new("Run history", "Run history per project, filter by operationId, export CSV, printable reports."),
                    new("Audit trail", "Audit trail for compliance, who ran what, when, and what changed."),
                    new("Built for CI", "Headless by default via MCP, no UI clicking required, consistent results.")
                },
                "Move from testing to evidence in one workflow.",
                "Create deterministic test plans, run them under policy, add AI coverage for edge cases, and share audit-ready proof.",
                "Request a demo",
                "/contact"),
            new PricingPageContent(
                "Pricing that supports delivery and governance",
                "Choose a plan that matches how you buy, individual validation, team QA, or compliance-led rollouts with AI evaluation and cost controls.",
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
                "Capture run history, explain flakiness, and export proof, plus use AI to expand coverage safely.",
                "Discuss QA workflows",
                "/contact"),
            new DeveloperDocsContent(
                "Developer docs",
                "Marketing-facing guidance for teams evaluating the MCP workflow, AI tools, and CI usage.",
                "Quickstart",
                docsQuickstartSteps,
                "MCP client overview",
                docsMcpHighlights,
                "CI usage narrative",
                docsCiSteps,
                new VisualPlaceholder(
                    "Developer workflow placeholder",
                    "Planned visual of MCP tool calls, CI runs, and AI grounded outputs."),
                "Start with MCP",
                "Set policy allowlists, run deterministic plans, ask grounded AI questions, and export evidence in minutes.",
                "Download the MCP client",
                "/contact"),
            new UseCasesContent(
                "Use cases",
                "Short stories from teams who need evidence they can trust, and AI explanations they can defend.",
                useCaseStories,
                "See your use case in a real workflow.",
                "We can share examples tied to deterministic test plans, run history, AI edge case discovery, and audit logs.",
                "Request examples",
                "/contact"),
            new AboutContent(
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
    string HeroTitle,
    string HeroSubtitle,
    string QuickstartTitle,
    IReadOnlyList<string> QuickstartSteps,
    string McpTitle,
    IReadOnlyList<string> McpHighlights,
    string CiTitle,
    IReadOnlyList<string> CiSteps,
    VisualPlaceholder WorkflowPlaceholder,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);

public sealed record UseCasesContent(
    string HeroTitle,
    string HeroSubtitle,
    IReadOnlyList<CaseStudy> CaseStudies,
    string CtaTitle,
    string CtaSubtitle,
    string CtaLabel,
    string CtaLink);

public sealed record CaseStudy(string Title, string Story);

public sealed record AboutContent(
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
