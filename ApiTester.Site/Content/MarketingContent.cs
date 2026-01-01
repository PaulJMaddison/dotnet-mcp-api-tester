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
                "Keep delivery moving with deterministic test plans and clear CI readiness signals.",
                "Use project separation to isolate services, and export run history to release notes.")
            ,
            new(
                "QA",
                "Standardise regression coverage with repeatable runs and clear failure context.",
                "Use run history timelines, flake notes, and audit logs to track decisions.")
            ,
            new(
                "Compliance",
                "Collect evidence without slowing teams or broadening access.",
                "Policy allowlists, SSRF guard controls, and immutable audit logs are built in.")
        };

        var homeWorkflowSteps = new List<WorkflowStep>
        {
            new(
                "Define the plan",
                "Create deterministic test plans per project, with policy allowlists and CI readiness rules."),
            new(
                "Run with controls",
                "Tests execute with SSRF guard protections, project separation, and scoped credentials."),
            new(
                "Share evidence",
                "Run history, audit logs, and exportable reports capture what changed and who approved it.")
        };

        var pricingPlans = new List<PricingPlan>
        {
            new(
                "Free",
                "For developers and fast-moving vibe coders validating a new API or integration.",
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
                    "Community support"
                }),
            new(
                "Pro",
                "For QA and product teams that need repeatable evidence every sprint.",
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
                    "Flaky handling notes and rerun controls",
                    "Audit logs and exports for stakeholders"
                }),
            new(
                "Team",
                "For regulated organisations requiring governance and separation across environments.",
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
                    "Priority support and onboarding"
                })
        };

        var pricingFeatureMatrix = new List<FeatureComparison>
        {
            new("Run history and evidence exports", new List<string> { "Limited", "Included", "Included" }),
            new("Deterministic test plans", new List<string> { "Included", "Included", "Included" }),
            new("Policy allowlists", new List<string> { "Basic", "Advanced", "Advanced" }),
            new("SSRF guard controls", new List<string> { "Standard", "Standard", "Custom" }),
            new("CI readiness checks", new List<string> { "Manual", "Automated", "Automated" })
        };

        var pricingUseCases = new List<UseCaseSummary>
        {
            new("Developer onboarding", "Connect a new API, validate contracts, and share proof with a single workspace."),
            new("QA regression", "Keep repeatable runs that map to deterministic test plans and release gates."),
            new("Compliance evidence", "Export audit logs and run history without exposing production secrets."),
            new("CI coverage", "Trigger suites in pipelines and publish CI readiness outcomes.")
        };

        var pricingFaqs = new List<FaqDefinition>
        {
            new("Can we change plans later?", "Yes. You can upgrade or downgrade at any time with pro-rated billing."),
            new("What does a run include?", "A run is a complete execution of a deterministic test plan, including retries within the same run."),
            new("Do you store payloads forever?", "No. Retention is configurable and can be set per project.")
        };

        var securityControls = new List<SecurityControl>
        {
            new("SSRF guard", "Outbound traffic is restricted to approved destinations and blocked from private, loopback, and metadata ranges."),
            new("Policy allowlists", "Allowlist policies define where tests can run, which methods are allowed, and which headers may be used."),
            new("Audit logs", "Every change, run, and approval is captured with actor, timestamp, and project context."),
            new("Project separation", "Projects isolate credentials, environments, and run history by default.")
        };

        var retentionNotes = new List<string>
        {
            "Retention windows are configurable per project and can be shortened or extended by policy.",
            "Evidence exports include run history, approvals, and configuration snapshots when available.",
            "We do not claim certifications we do not hold; ask for the latest assurance pack."
        };

        var qaHighlights = new List<QaHighlight>
        {
            new("Run history", "Track every run with timestamps, owners, and environment markers for audit-ready traceability."),
            new("Repeatability", "Deterministic test plans ensure the same inputs and assertions can be rerun on demand."),
            new("Flaky handling", "Mark flakiness, capture notes, and rerun without rewriting the plan. Automated retries are configurable."),
            new("Exports and evidence", "Export CSV and JSON summaries for QA sign-off and compliance archives.")
        };

        var qaWorkflowSteps = new List<WorkflowStep>
        {
            new("Create deterministic plans", "Define fixtures, preconditions, and assertions once per project."),
            new("Run in CI", "Trigger plans from your pipeline and capture CI readiness in the run history."),
            new("Share evidence", "Attach audit logs and exports to release sign-off packs.")
        };

        var docsQuickstartSteps = new List<string>
        {
            "Create a project and set policy allowlists for target environments.",
            "Connect your OpenAPI or GraphQL spec and map base URLs.",
            "Build a deterministic test plan and run it locally or in CI.",
            "Review run history and export evidence for QA or compliance."
        };

        var docsMcpHighlights = new List<string>
        {
            "The MCP client runs locally and sends execution metadata to ApiTester.",
            "SSRF guard rules are enforced before outbound requests are made.",
            "Run results stream back as structured run history with audit logs."
        };

        var docsCiSteps = new List<string>
        {
            "Generate a token scoped to the project and environment.",
            "Run the MCP client in your pipeline to execute deterministic test plans.",
            "Publish CI readiness status and export artefacts for release notes."
        };

        var useCaseStories = new List<CaseStudy>
        {
            new(
                "Dev lead",
                "A platform engineer needed quick feedback when APIs changed. ApiTester kept specs and tests aligned, then surfaced CI readiness before merging. The team relied on run history to prove what was tested without opening production access."),
            new(
                "QA lead",
                "A QA team replaced manual spreadsheets with deterministic test plans and repeatable runs. Flaky handling notes helped prioritise fixes, while exports provided evidence for weekly release sign-off."),
            new(
                "Compliance reviewer",
                "A compliance reviewer required project separation, policy allowlists, and clear audit logs. ApiTester supplied evidence packs that showed who ran what, when, and under which policy." )
        };

        var aboutPrinciples = new List<string>
        {
            "Deliver outcomes before features: evidence, confidence, and controlled access.",
            "Keep controls visible: SSRF guard, policy allowlists, audit logs, and project separation are not optional add-ons.",
            "Stay practical: deterministic test plans and CI readiness reduce debate during releases."
        };

        var aboutAudience = new List<string>
        {
            "Developers who want clear feedback without slowing delivery.",
            "QA teams who need repeatable evidence with run history.",
            "Compliance and security teams who need policy-backed controls and audit logs.",
            "Fast-moving teams and vibe coders who want a clear path to safe releases."
        };

        var aboutNotList = new List<string>
        {
            "ApiTester is not a production traffic proxy or API gateway.",
            "It does not scan for vulnerabilities or replace a security testing programme.",
            "It does not execute arbitrary outbound calls outside policy allowlists.",
            "It is not a general-purpose ticketing or incident management tool."
        };

        return new MarketingContent(
            new MetadataContent(
                "ApiTester",
                "Outcome-led API testing for developers, QA, and compliance teams.",
                "API testing, deterministic test plans, audit logs, CI readiness, SSRF guard",
                "https://apitester.example.com",
                "ApiTester - API testing with evidence",
                "Run deterministic API tests with audit logs, policy allowlists, and CI readiness signals.",
                "summary_large_image",
                "/images/hero-illustration.svg",
                "Illustration of the ApiTester testing workspace"),
            new LayoutContent(
                "ApiTester",
                "Outcome-led API testing with deterministic plans, run history, and audit-grade evidence.",
                navigationItems,
                footerGroups,
                "Email: support@apitester.example.com",
                "Phone: +44 (0)20 7946 0123",
                "Location: 21 King Street, London, UK",
                "© 2024 ApiTester. All rights reserved."),
            new HomePageContent(
                "Deliver release-ready API evidence without slowing delivery.",
                "ApiTester gives developers, QA, and compliance teams deterministic test plans, run history, and CI readiness without opening risky access.",
                "Book a walkthrough",
                "/contact",
                "Read developer docs",
                "/docs",
                new VisualPlaceholder(
                    "Hero visual placeholder",
                    "Planned visual of deterministic plans, run history, and CI readiness."),
                "Outcomes for every role",
                homePersonaTiles,
                "Workflow",
                "How teams plan, run, and evidence API testing.",
                homeWorkflowSteps,
                new VisualPlaceholder(
                    "Workflow animation placeholder",
                    "Planned visual showing plan, run, and evidence handover."),
                "Proof points",
                new List<ProofPoint>
                {
                    new("SSRF guard and policy allowlists", "Controls for where tests can call out."),
                    new("Audit logs and project separation", "Every action is traceable and scoped."),
                    new("Run history and CI readiness", "Clear evidence before release sign-off.")
                },
                "Move from tests to evidence in one workflow.",
                "Create deterministic test plans, run them under policy, and share audit-ready proof.",
                "Request a demo",
                "/contact"),
            new PricingPageContent(
                "Pricing that supports delivery and governance",
                "Choose a plan that matches how you buy: individual validation, team QA, or compliance-led rollouts.",
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
                    "SSRF guard defaults and policy allowlists"
                },
                new VisualPlaceholder(
                    "Pricing visual placeholder",
                    "Planned comparison visual for plan coverage and evidence."),
                "Compare plans",
                "Plan features aligned to delivery and compliance needs.",
                "Capability",
                "Ask about governance",
                "/contact",
                pricingFeatureMatrix,
                "Teams using ApiTester",
                "Short examples of how teams adopt consistent API evidence.",
                pricingUseCases,
                "Pricing FAQs",
                "Clear answers to common buying questions.",
                pricingFaqs),
            new SecurityComplianceContent(
                "Security & compliance",
                "Controls are built in so teams can run tests without broad access or hidden risk.",
                "Security controls",
                securityControls,
                "Retention and evidence",
                retentionNotes,
                new VisualPlaceholder(
                    "Security controls placeholder",
                    "Planned visual for SSRF guard and allowlist policy flows."),
                "Need a security review?",
                "We can share our latest control summary, data handling notes, and audit log samples.",
                "Contact security",
                "/contact"),
            new QaReportingContent(
                "QA & reporting",
                "Repeatable API evidence with run history, deterministic test plans, and exportable reports.",
                "QA outcomes",
                qaHighlights,
                "Workflow",
                "A predictable path from plan to evidence.",
                qaWorkflowSteps,
                new VisualPlaceholder(
                    "QA workflow placeholder",
                    "Planned visual for run history and evidence exports."),
                "Turn QA work into evidence, not noise.",
                "Capture run history, explain flakiness, and export proof without spreadsheets.",
                "Discuss QA workflows",
                "/contact"),
            new DeveloperDocsContent(
                "Developer docs",
                "Marketing-facing guidance for teams evaluating the MCP client and CI usage.",
                "Quickstart",
                docsQuickstartSteps,
                "MCP client overview",
                docsMcpHighlights,
                "CI usage narrative",
                docsCiSteps,
                new VisualPlaceholder(
                    "Developer workflow placeholder",
                    "Planned visual of MCP client runs and CI readiness signals."),
                "Start with the MCP client",
                "Set policy allowlists, run deterministic plans, and export CI evidence in minutes.",
                "Download the MCP client",
                "/contact"),
            new UseCasesContent(
                "Use cases",
                "Short stories from teams who need evidence they can trust.",
                useCaseStories,
                "See your use case in a real workflow.",
                "We can share examples tied to deterministic test plans, run history, and audit logs.",
                "Request examples",
                "/contact"),
            new AboutContent(
                "About ApiTester",
                "We build for teams who need practical evidence, not noise.",
                "Product philosophy",
                aboutPrinciples,
                "Who it’s for",
                aboutAudience,
                "What it is not",
                aboutNotList,
                "Work with a product that stays within policy.",
                "We help teams show evidence, not shortcuts, and we are clear about the limits.",
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
