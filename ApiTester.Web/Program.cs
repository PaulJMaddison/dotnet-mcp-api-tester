using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiTester.Web;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Services;
using ApiTester.Web.Observability;
using ApiTester.Web.Contracts;
using ApiTester.Web.Comparison;
using ApiTester.Web.Execution;
using ApiTester.Web.Auth;
using ApiTester.Web.AI;
using ApiTester.Web.Billing;
using ApiTester.Web.Diff;
using ApiTester.Web.Mapping;
using ApiTester.Web.Reports;
using ApiTester.Web.Validation;
using ApiTester.Web.AbuseProtection;
using ApiTester.Web.Jobs;
using ApiTester.Web.Errors;
using ApiTester.AI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using WebOpenApiImportLimits = ApiTester.Web.OpenApiImportLimits;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = WebOpenApiImportLimits.MaxRequestBodyBytes;
});

var appConfig = AppConfig.Load(builder.Configuration);
builder.Services.AddSingleton(appConfig);

builder.Services.AddApiTesterPersistence(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApiTesterTelemetry>();

builder.Services.Configure<ExecutionOptions>(builder.Configuration.GetSection("Execution"));
builder.Services.Configure<CleanupJobOptions>(builder.Configuration.GetSection("CleanupJobs"));
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ProjectContext>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<AbuseRateLimitOptions>(builder.Configuration.GetSection("AbuseProtection:RateLimits"));
builder.Services.AddSingleton<TenantIpRateLimiter>();
builder.Services.AddScoped<ApiRuntimeConfig>(sp =>
{
    var options = sp.GetRequiredService<IOptions<ExecutionOptions>>().Value;
    var runtime = new ApiRuntimeConfig();

    ApiPolicyDefaults.ApplySafeDefaults(runtime.Policy);
    runtime.Policy.DryRun = options.DryRun ?? false;
    runtime.Policy.HostedMode = builder.Configuration.GetValue<bool>("Security:HostedMode") || options.HostedMode;

    if (options.AllowedBaseUrls.Count > 0)
    {
        runtime.Policy.AllowedBaseUrls.Clear();
        foreach (var url in options.AllowedBaseUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            runtime.Policy.AllowedBaseUrls.Add(url.Trim());
    }

    if (options.AllowedMethods.Count > 0)
    {
        runtime.Policy.AllowedMethods.Clear();
        foreach (var method in options.AllowedMethods.Where(m => !string.IsNullOrWhiteSpace(m)))
            runtime.Policy.AllowedMethods.Add(method.Trim());
    }

    if (options.BlockLocalhost.HasValue)
        runtime.Policy.BlockLocalhost = options.BlockLocalhost.Value;

    if (options.BlockPrivateNetworks.HasValue)
        runtime.Policy.BlockPrivateNetworks = options.BlockPrivateNetworks.Value;

    if (options.TimeoutSeconds.HasValue && options.TimeoutSeconds.Value > 0)
        runtime.Policy.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds.Value);

    if (options.MaxRequestBodyBytes.HasValue && options.MaxRequestBodyBytes.Value > 0)
        runtime.Policy.MaxRequestBodyBytes = options.MaxRequestBodyBytes.Value;

    if (options.MaxResponseBodyBytes.HasValue && options.MaxResponseBodyBytes.Value > 0)
        runtime.Policy.MaxResponseBodyBytes = options.MaxResponseBodyBytes.Value;

    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        runtime.SetBaseUrl(options.BaseUrl);

    return runtime;
});
builder.Services.AddScoped<TestPlanRunner>();
builder.Services.AddHttpClient(TestPlanRunner.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false
    });
builder.Services.AddHttpClient();
builder.Services.AddSingleton<RunComparisonService>();
builder.Services.AddScoped<BaselineComparisonService>();
builder.Services.AddSingleton<IAiClient, NullAiClient>();
builder.Services.Configure<AiRateLimitOptions>(builder.Configuration.GetSection("AI:RateLimits"));
builder.Services.Configure<OpenAiProviderOptions>(builder.Configuration.GetSection("AI:OpenAI"));
builder.Services.AddSingleton<AiRateLimiter>();
builder.Services.AddHttpClient(nameof(OpenAiProvider));
builder.Services.AddSingleton<IAiProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiProviderOptions>>().Value;
    var configuredKey = string.IsNullOrWhiteSpace(options.ApiKey)
        ? builder.Configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        : options.ApiKey;

    if (string.IsNullOrWhiteSpace(configuredKey))
        return new StubAiProvider();

    options.ApiKey = configuredKey;
    return ActivatorUtilities.CreateInstance<OpenAiProvider>(sp);
});
builder.Services.AddScoped<AiAnalysisService>();
builder.Services.AddScoped<AiExplainService>();
builder.Services.AddScoped<AiDocsGenerationService>();
builder.Services.AddScoped<AiRunSummaryService>();
builder.Services.AddScoped<AiSuggestTestsService>();
var stripeOptions = builder.Configuration.GetSection(StripeBillingOptions.SectionName).Get<StripeBillingOptions>() ?? new StripeBillingOptions();
var stripeBillingConfigured = !string.IsNullOrWhiteSpace(stripeOptions.SecretKey)
    && !string.IsNullOrWhiteSpace(stripeOptions.WebhookSecret);

builder.Services.Configure<StripeBillingOptions>(builder.Configuration.GetSection(StripeBillingOptions.SectionName));
builder.Services.AddScoped<StripeBillingService>();
builder.Services.AddScoped<SubscriptionEnforcementService>();
builder.Services.AddScoped<RunCleanupCoordinator>();
builder.Services.AddScoped<OrgContextResolver>();
builder.Services.AddScoped<IApiTokenService, ApiTokenService>();
builder.Services.AddScoped<IRetentionPruner, RetentionPruner>();
builder.Services.AddScoped<ITenantContext>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (httpContext?.Items.TryGetValue(TenantContextMiddleware.ContextItemName, out var value) == true
        && value is ITenantContext tenantContext)
    {
        return tenantContext;
    }

    return new TenantContext(OrgDefaults.DefaultOrganisationId);
});

builder.Services.AddHostedService<RetentionCleanupHostedService>();
builder.Services.AddHostedService<ResponseSnippetCleanupHostedService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};

var app = builder.Build();
var appVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
var isDevelopment = app.Environment.IsDevelopment();

var exceptionLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("ApiTester.Web.Exceptions");

app.UseExceptionHandler(config =>
{
    config.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        exceptionLogger.LogError(exception, "Unhandled exception while processing request.");

        await ApiProblemFactory.Result(context, StatusCodes.Status500InternalServerError, "UnhandledServerError", "Server error", "An unexpected error occurred.").ExecuteAsync(context);
    });
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ApiKeyRedactionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health")
        && !context.Request.Path.StartsWithSegments("/api/v1/admin")
        && !context.Request.Path.StartsWithSegments("/api/v1/billing/webhook"),
    builder =>
    {
        builder.UseMiddleware<ApiKeyAuthMiddleware>();
        builder.UseMiddleware<TenantContextMiddleware>();
        builder.UseMiddleware<OrgContextMiddleware>();
        builder.UseMiddleware<TenantIpRateLimitMiddleware>();
    });

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var limit = path.EndsWith("/openapi/import", StringComparison.OrdinalIgnoreCase)
            ? WebOpenApiImportLimits.MaxRequestBodyBytes
            : RequestBodyLimits.MaxRequestBodyBytes;

        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > limit)
        {
            await ApiProblemFactory.Result(context, StatusCodes.Status413PayloadTooLarge, "RequestBodyTooLarge", "Request too large", $"Request body must be <= {limit} bytes.").ExecuteAsync(context);
            return;
        }

        await next();
    });
});

app.MapGet("/health", (IHostEnvironment env, IOptions<PersistenceOptions> options) =>
{
    var persistence = options.Value;
    return Results.Ok(new
    {
        env = env.EnvironmentName,
        provider = (persistence.Provider ?? "File").Trim(),
        hasConnectionString = !string.IsNullOrWhiteSpace(persistence.ConnectionString)
    });
});

app.MapGet("/api/version", () => Results.Ok(new VersionResponse(appVersion)));

using (var scope = app.Services.CreateScope())
{
    var persistenceOptions = scope.ServiceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var selection = PersistenceProviderSelector.Select(persistenceOptions);

    if (selection.UseSqlProvider)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        dbContext.Database.Migrate();
    }
}

var v1 = app.MapGroup("/api/v1");


v1.MapGet("/tokens", async (IApiKeyStore apiKeyStore, HttpContext httpContext, CancellationToken ct) =>
{
    var tenantContext = httpContext.GetTenantContext();
    var keys = await apiKeyStore.ListAsync(tenantContext.TenantId, ct);
    return Results.Ok(ApiKeyMapping.ToListResponse(keys));
});

v1.MapPost("/tokens", async (ApiKeyCreateRequest request, IApiTokenService apiTokenService, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, RequestValidation.MaxNameLength, out var error))
        return InvalidRequest(httpContext, error);

    if (request.Scopes is null || request.Scopes.Count == 0)
        return InvalidRequest("At least one scope is required.");

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var created = await apiTokenService.CreateTokenAsync(tenantContext.TenantId, orgContext.UserId, request.Name, request.Scopes, request.ExpiresUtc, ct);

    var metadataJson = JsonSerializer.Serialize(new
    {
        created.Record.Name,
        created.Record.Scopes,
        created.Record.ExpiresUtc,
        created.Record.Prefix
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ApiKeyCreated,
        "api_key",
        created.Record.KeyId.ToString(),
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(new ApiKeyCreateResponse(ApiKeyMapping.ToDto(created.Record), created.Token));
});

v1.MapPost("/tokens/{id}/revoke", async (string id, IApiKeyStore apiKeyStore, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(id, out var keyId, out var error))
        return InvalidRequest(httpContext, error);

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var record = await apiKeyStore.RevokeAsync(tenantContext.TenantId, keyId, DateTime.UtcNow, ct);
    if (record is null)
        return Results.NotFound();

    var metadataJson = JsonSerializer.Serialize(new
    {
        record.Name,
        record.RevokedUtc
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ApiKeyRevoked,
        "api_key",
        record.KeyId.ToString(),
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(ApiKeyMapping.ToDto(record));
});

v1.MapGet("/me", async (SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var snapshot = await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct);

    return Results.Ok(new
    {
        user = new { orgContext.UserId, orgContext.Role },
        tenant = new { tenantId = tenantContext.TenantId },
        plan = new
        {
            name = snapshot.Subscription.Plan.ToString(),
            status = snapshot.Subscription.Status.ToString(),
            renews = snapshot.Subscription.Renews,
            periodStartUtc = snapshot.Subscription.PeriodStartUtc,
            periodEndUtc = snapshot.Subscription.PeriodEndUtc
        }
    });
});

v1.MapGet("/billing/plan", async (SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!stripeBillingConfigured)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});
    var tenantContext = httpContext.GetTenantContext();
    var snapshot = await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct);
    var limits = new BillingPlanLimits(
        snapshot.Limits.MaxProjects,
        snapshot.Limits.MaxRunsPerPeriod,
        snapshot.Limits.MaxAiCallsPerPeriod);

    var response = new BillingPlanResponse(
        snapshot.Subscription.Plan.ToString(),
        snapshot.Subscription.Status.ToString(),
        snapshot.Subscription.Renews,
        snapshot.Subscription.PeriodStartUtc,
        snapshot.Subscription.PeriodEndUtc,
        snapshot.Limits.RetentionDays,
        snapshot.Limits.AiEnabled,
        snapshot.Limits.AuditExportEnabled,
        limits);

    return Results.Ok(response);
});

v1.MapGet("/billing/usage", async (IProjectStore projectStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!stripeBillingConfigured)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});
    var tenantContext = httpContext.GetTenantContext();
    var totalProjects = await GetProjectCountAsync(projectStore, tenantContext.TenantId, ct);
    await subscriptions.UpdateProjectsUsedAsync(tenantContext.TenantId, totalProjects, ct);

    var snapshot = await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct);
    var usageCounter = await subscriptions.GetUsageAsync(tenantContext.TenantId, ct);
    var limits = new BillingPlanLimits(
        snapshot.Limits.MaxProjects,
        snapshot.Limits.MaxRunsPerPeriod,
        snapshot.Limits.MaxAiCallsPerPeriod);

    var usage = new BillingUsageCounters(
        totalProjects,
        usageCounter.RunsUsed,
        usageCounter.AiCallsUsed,
        usageCounter.ExportsUsed);

    var response = new BillingUsageResponse(
        usageCounter.PeriodStartUtc,
        usageCounter.PeriodEndUtc,
        limits,
        usage);

    return Results.Ok(response);
});

v1.MapPost("/billing/checkout", async (BillingCheckoutRequest request, StripeBillingService stripeBilling, IOptions<StripeBillingOptions> stripeOptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!stripeBillingConfigured)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});
    if (!stripeOptions.Value.BillingEnabled)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});

    var desired = (request?.Plan ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(desired))
        return InvalidRequest("plan is required.");

    var tenantContext = httpContext.GetTenantContext();
    var url = await stripeBilling.CreateCheckoutSessionAsync(tenantContext.TenantId, desired, ct);
    return Results.Ok(new BillingCheckoutResponse(url));
});

v1.MapPost("/billing/portal", async (StripeBillingService stripeBilling, IOptions<StripeBillingOptions> stripeOptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!stripeBillingConfigured)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});
    if (!stripeOptions.Value.BillingEnabled)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});

    var tenantContext = httpContext.GetTenantContext();
    var url = await stripeBilling.CreatePortalSessionAsync(tenantContext.TenantId, ct);
    return Results.Ok(new BillingPortalResponse(url));
});

v1.MapPost("/billing/webhook", async (HttpContext httpContext, StripeBillingService stripeBilling, IOptions<StripeBillingOptions> stripeOptions, CancellationToken ct) =>
{
    if (!stripeBillingConfigured)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});
    if (!stripeOptions.Value.WebhookEnabled)
        return BillingNotConfigured(httpContext, new[]{"Billing:Stripe:SecretKey","Billing:Stripe:WebhookSecret"});

    httpContext.Request.EnableBuffering();
    using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
    var payload = await reader.ReadToEndAsync();
    httpContext.Request.Body.Position = 0;
    var signature = httpContext.Request.Headers["Stripe-Signature"].ToString();
    if (string.IsNullOrWhiteSpace(signature))
        return InvalidRequest("Stripe-Signature header is required.");

    var processed = await stripeBilling.HandleWebhookAsync(payload, signature, ct);
    return processed ? Results.Ok() : Results.Accepted();
}).AllowAnonymous();

v1.MapPost("/admin/tenants", async (AdminTenantCreateRequest request, IOrganisationStore orgStore, IUserStore userStore, IMembershipStore membershipStore, IHostEnvironment env, CancellationToken ct) =>
{
    if (!env.IsDevelopment())
        return Results.NotFound();

    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var nameError))
        return InvalidRequest(nameError);

    if (!RequestValidation.TryValidateRequiredKey(request?.Slug, "Slug", RequestValidation.MaxSlugLength, out var slugError))
        return InvalidRequest(slugError);

    if (!RequestValidation.TryValidateRequiredKey(request?.OwnerExternalId, "OwnerExternalId", RequestValidation.MaxExternalIdLength, out var ownerError))
        return InvalidRequest(ownerError);

    var displayName = string.IsNullOrWhiteSpace(request?.OwnerDisplayName)
        ? request.OwnerExternalId
        : request.OwnerDisplayName.Trim();

    if (!RequestValidation.TryValidateRequiredKey(displayName, "OwnerDisplayName", RequestValidation.MaxDisplayNameLength, out var displayError))
        return InvalidRequest(displayError);

    if (!RequestValidation.TryNormalizeOptionalValue(request?.OwnerEmail, RequestValidation.MaxEmailLength, out var email, out var emailError))
        return InvalidRequest(emailError);

    var org = await orgStore.CreateAsync(request!.Name, request.Slug, ct);
    var user = await userStore.CreateAsync(request.OwnerExternalId, displayName, email, ct);
    await membershipStore.CreateAsync(org.OrganisationId, user.UserId, OrgRole.Owner, ct);

    return Results.Ok(new AdminTenantCreateResponse(
        OrgMapping.ToDto(org),
        new AdminUserDto(user.UserId, user.ExternalId, user.DisplayName, user.Email)));
});

v1.MapPost("/admin/apikeys", async (AdminApiKeyCreateRequest request, IApiKeyStore apiKeyStore, IOrganisationStore orgStore, IUserStore userStore, IMembershipStore membershipStore, IHostEnvironment env, CancellationToken ct) =>
{
    if (!env.IsDevelopment())
        return Results.NotFound();

    if (request is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryValidateRequiredName(request.Name, RequestValidation.MaxNameLength, out var nameError))
        return InvalidRequest(nameError);

    if (!ApiKeyScopes.TryNormalize(request.Scopes, out var normalizedScopes, out var scopeError))
        return InvalidRequest(scopeError);

    if (request.ExpiresUtc is { } expiresUtc && expiresUtc <= DateTime.UtcNow)
        return InvalidRequest("ExpiresUtc must be in the future.");

    if (request.OrganisationId == Guid.Empty)
        return InvalidRequest("OrganisationId is required.");

    if (request.UserId == Guid.Empty)
        return InvalidRequest("UserId is required.");

    var org = await orgStore.GetAsync(request.OrganisationId, ct);
    if (org is null)
        return Results.NotFound();

    var user = await userStore.GetAsync(request.UserId, ct);
    if (user is null)
        return Results.NotFound();

    var membership = await membershipStore.GetAsync(org.OrganisationId, user.UserId, ct);
    if (membership is null)
        return InvalidRequest("User is not a member of the organisation.");

    var token = ApiKeyToken.Generate();
    var hash = ApiKeyHasher.Hash(token.Token);
    var record = await apiKeyStore.CreateAsync(
        org.OrganisationId,
        user.UserId,
        request.Name,
        ApiKeyScopes.Serialize(normalizedScopes),
        request.ExpiresUtc,
        hash,
        token.Prefix,
        ct);

    return Results.Ok(new ApiKeyCreateResponse(ApiKeyMapping.ToDto(record), token.Token));
});

v1.MapGet("/projects", async (int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, IProjectStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryNormalizePageSize(pageSize, take, 50, 1, 200, out var normalizedPageSize, out var sizeError))
        return InvalidRequest(sizeError);

    if (!RequestValidation.TryNormalizePageToken(pageToken, skip, out var offset, out var tokenError))
        return InvalidRequest(tokenError);

    if (!RequestValidation.TryNormalizeSort(sort, SortField.CreatedUtc, out var sortField, out var sortError))
        return InvalidRequest(sortError);

    if (!RequestValidation.TryNormalizeOrder(order, SortDirection.Desc, out var direction, out var orderError))
        return InvalidRequest(orderError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var result = await store.ListAsync(tenantContext.TenantId, new PageRequest(normalizedPageSize, offset), sortField, direction, ct);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(ProjectMapping.ToListResponse(metadata, result.Items));
});

v1.MapPost("/projects", async (ProjectCreateRequest request, IProjectStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, RequestValidation.MaxNameLength, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var gate = await subscriptions.CheckProjectCreateAsync(tenantContext.TenantId, ct);
    if (!gate.Allowed)
        return SubscriptionProblem(gate);

    var project = await store.CreateAsync(tenantContext.TenantId, orgContext.OwnerKey, request!.Name!, ct);
    var totalProjects = await GetProjectCountAsync(store, tenantContext.TenantId, ct);
    await subscriptions.UpdateProjectsUsedAsync(tenantContext.TenantId, totalProjects, ct);
    logger.LogInformation("Created project {ProjectId} for org {OrganisationId} with name {ProjectName}", project.ProjectId, tenantContext.TenantId, project.Name);
    return Results.Ok(ProjectMapping.ToDto(project));
});

v1.MapGet("/projects/current", (ProjectContext context, HttpContext httpContext) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    return Results.Ok(new ProjectCurrentResponse(context.CurrentProjectId));
});

v1.MapPut("/projects/current", async (ProjectCurrentRequest request, IProjectStore store, ProjectContext context, HttpContext httpContext, CancellationToken ct) =>
{
    if (request is null || request.ProjectId == Guid.Empty)
        return InvalidRequest("projectId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await store.GetAsync(tenantContext.TenantId, request.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(store, tenantContext, request.ProjectId, ct);

    context.SetCurrentProject(request.ProjectId);
    return Results.Ok(new ProjectCurrentResponse(context.CurrentProjectId));
});

v1.MapPost("/projects/{projectId}/openapi/import", ImportOpenApiSpecAsync);
v1.MapPost("/projects/{projectId}/specs/import", ImportOpenApiSpecAsync);

v1.MapGet("/projects/{projectId}/specs", async (string projectId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var records = await specStore.ListAsync(tenantContext.TenantId, id, ct);
    return Results.Ok(records.Select(OpenApiMapping.ToMetadataDto));
});


v1.MapGet("/projects/{projectId}/specs/{specId}", async (string projectId, string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(specId, out var specGuid, out var specError))
        return InvalidRequest(specError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var record = await specStore.GetByIdAsync(tenantContext.TenantId, specGuid, ct);
    if (record is null || record.ProjectId != id)
        return Results.NotFound();

    return Results.Ok(OpenApiMapping.ToMetadataDto(record));
});

v1.MapGet("/projects/{projectId}/specs/diff", async (string projectId, string? from, string? to, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        return InvalidRequest("Both from and to spec ids are required.");

    if (!RequestValidation.TryParseGuid(from, out var specAId, out var specAError))
        return InvalidRequest(specAError);

    if (!RequestValidation.TryParseGuid(to, out var specBId, out var specBError))
        return InvalidRequest(specBError);

    var recordA = await specStore.GetByIdAsync(tenantContext.TenantId, specAId, ct);
    if (recordA is null || recordA.ProjectId != id)
        return Results.NotFound();

    var recordB = await specStore.GetByIdAsync(tenantContext.TenantId, specBId, ct);
    if (recordB is null || recordB.ProjectId != id)
        return Results.NotFound();

    var reader = new OpenApiStringReader();
    var docA = reader.Read(recordA.SpecJson, out _);
    if (docA is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Spec A could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var docB = reader.Read(recordB.SpecJson, out _);
    if (docB is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Spec B could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var diff = OpenApiDiffEngine.Diff(docA, docB);
    var response = new OpenApiDiffResponse(
        recordA.SpecId,
        recordB.SpecId,
        diff.Items.Select(item => new OpenApiDiffItemDto(
            item.Classification.ToString(),
            item.Change,
            item.Path,
            item.Method,
            item.Detail)).ToList());

    return Results.Ok(response);
});

v1.MapDelete("/projects/{projectId}/specs/{specId}", async (string projectId, string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(specId, out var specGuid, out var specError))
        return InvalidRequest(specError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var record = await specStore.GetByIdAsync(tenantContext.TenantId, specGuid, ct);
    if (record is null || record.ProjectId != id)
        return Results.NotFound();

    var removed = await specStore.DeleteAsync(tenantContext.TenantId, specGuid, ct);
    return removed ? Results.NoContent() : Results.NotFound();
});

v1.MapGet("/projects/{projectId}/operations", async (string projectId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before listing operations.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var operations = EnumerateOperations(doc)
        .Select(op => new OpenApiOperationSummaryDto(
            op.OperationId,
            op.Method.ToString().ToUpperInvariant(),
            op.Path,
            op.Operation.Summary ?? string.Empty,
            op.Operation.Description ?? string.Empty,
            op.Operation.Security is { Count: > 0 }))
        .OrderBy(op => op.Path)
        .ThenBy(op => op.Method)
        .ToList();

    return Results.Ok(new OpenApiOperationListResponse(operations));
});

v1.MapGet("/projects/{projectId}/operations/describe", async (string projectId, string? operationId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before describing operations.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var match = EnumerateOperations(doc)
        .FirstOrDefault(op => string.Equals(op.OperationId, operationId.Trim(), StringComparison.OrdinalIgnoreCase));

    if (match == default)
        return Results.NotFound();

    var parameters = (match.Operation.Parameters ?? new List<OpenApiParameter>())
        .Select(param =>
        {
            var location = (ParameterLocation?)param.In;
            return new OpenApiOperationParameterDto(
                param.Name,
                location?.ToString() ?? string.Empty,
                param.Required,
                param.Description ?? string.Empty,
                DescribeSchema(param.Schema));
        })
        .ToList();

    OpenApiOperationRequestBodyDto? requestBody = null;
    if (match.Operation.RequestBody is not null)
    {
        requestBody = new OpenApiOperationRequestBodyDto(
            match.Operation.RequestBody.Required,
            match.Operation.RequestBody.Description ?? string.Empty,
            match.Operation.RequestBody.Content.ToDictionary(
                kv => kv.Key,
                kv => new OpenApiOperationContentDto(DescribeSchema(kv.Value.Schema))));
    }

    var responses = match.Operation.Responses.ToDictionary(
        kv => kv.Key,
        kv => new OpenApiOperationResponseDto(
            kv.Value.Description ?? string.Empty,
            (kv.Value.Content ?? new Dictionary<string, OpenApiMediaType>()).ToDictionary(
                content => content.Key,
                content => new OpenApiOperationContentDto(DescribeSchema(content.Value.Schema)))));

    var response = new OpenApiOperationDescribeResponse(
        match.OperationId,
        match.Method.ToString().ToUpperInvariant(),
        match.Path,
        match.Operation.Summary ?? string.Empty,
        match.Operation.Description ?? string.Empty,
        match.Operation.Security is { Count: > 0 },
        parameters,
        requestBody,
        responses);

    return Results.Ok(response);
});

v1.MapGet("/runtime/policy", (ApiRuntimeConfig runtime, HttpContext httpContext) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    return Results.Ok(ToPolicyResponse(runtime.Policy));
});

v1.MapPut("/runtime/policy", async (ApiPolicyUpdateRequest request, ApiRuntimeConfig runtime, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (request is null)
        return InvalidRequest("Request body is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    if (!TryApplyPolicyUpdate(runtime.Policy, request, out var error))
        return InvalidRequest(httpContext, error);

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var metadataJson = JsonSerializer.Serialize(ToPolicyResponse(runtime.Policy), jsonOptions);
    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.PolicySet,
        "policy",
        "runtime",
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(ToPolicyResponse(runtime.Policy));
});

v1.MapPost("/runtime/policy/reset", async (ApiRuntimeConfig runtime, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    ApiPolicyDefaults.ApplySafeDefaults(runtime.Policy);

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var metadataJson = JsonSerializer.Serialize(ToPolicyResponse(runtime.Policy), jsonOptions);
    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.PolicyReset,
        "policy",
        "runtime",
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(ToPolicyResponse(runtime.Policy));
});

v1.MapGet("/runtime/base-url", (ApiRuntimeConfig runtime, HttpContext httpContext) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    return Results.Ok(new ApiRuntimeBaseUrlResponse(runtime.BaseUrl));
});

v1.MapPut("/runtime/base-url", async (ApiRuntimeBaseUrlRequest request, ApiRuntimeConfig runtime, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryNormalizeBaseUrl(request?.BaseUrl, out var normalizedBaseUrl, out var baseUrlError))
        return InvalidRequest(baseUrlError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    runtime.SetBaseUrl(normalizedBaseUrl);

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var metadataJson = JsonSerializer.Serialize(new { baseUrl = runtime.BaseUrl }, jsonOptions);
    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.BaseUrlSet,
        "runtime",
        "base_url",
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(new ApiRuntimeBaseUrlResponse(runtime.BaseUrl));
});

v1.MapPost("/runtime/auth/bearer", (ApiRuntimeBearerTokenRequest request, ApiRuntimeConfig runtime, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(request?.Token))
        return InvalidRequest("token is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    runtime.SetBearerToken(request.Token.Trim());
    return Results.Ok(new ApiRuntimeAuthResponse(true, runtime.BearerToken is not null));
});

v1.MapDelete("/runtime/auth/bearer", (ApiRuntimeConfig runtime, HttpContext httpContext) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    runtime.ClearAuth();
    return Results.Ok(new ApiRuntimeAuthResponse(true, runtime.BearerToken is not null));
});

v1.MapPost("/runtime/reset", async (ApiRuntimeConfig runtime, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    runtime.ResetRuntime();

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var metadataJson = JsonSerializer.Serialize(ToPolicyResponse(runtime.Policy), jsonOptions);
    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.PolicyReset,
        "policy",
        "runtime",
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(new ApiRuntimeResetResponse(runtime.BaseUrl, runtime.BearerToken is not null, ToPolicyResponse(runtime.Policy)));
});

v1.MapPost("/projects/{projectId}/testplans/{operationId}/generate", async (
    string projectId,
    string operationId,
    IProjectStore projectStore,
    IOrganisationStore orgStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    RedactionService redactionService,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before generating a test plan.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var match = FindOperation(doc, operationId);
    if (match is null)
        return Results.NotFound();

    var (path, method, op) = match.Value;
    var plan = TestPlanFactory.Create(op, method, path, operationId.Trim());
    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var redactedPlan = redactionService.RedactPlan(plan, org?.RedactionRules);
    var planJson = JsonSerializer.Serialize(redactedPlan, jsonOptions);

    var record = await planStore.UpsertAsync(id, operationId.Trim(), planJson, DateTime.UtcNow, ct);
    return Results.Ok(new TestPlanResponse(record.ProjectId, record.OperationId, record.PlanJson, record.CreatedUtc));
});

v1.MapPost("/projects/{projectId}/testplans/{operationId}/run", async (
    string projectId,
    string operationId,
    string? environment,
    IProjectStore projectStore,
    IOrganisationStore orgStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    IEnvironmentStore environmentStore,
    SubscriptionEnforcementService subscriptions,
    TestPlanRunner runner,
    ApiRuntimeConfig runtime,
    RedactionService redactionService,
    IAuditEventStore auditStore,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var runGate = await subscriptions.TryConsumeRunAsync(tenantContext.TenantId, ct);
    if (!runGate.Allowed)
        return SubscriptionProblem(runGate);

    var spec = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before executing runs.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    if (!RequestValidation.TryNormalizeOptionalValue(environment, out var environmentName, out var environmentError))
        return InvalidRequest(environmentError);

    string? environmentBaseUrl = null;
    if (!string.IsNullOrWhiteSpace(environmentName))
    {
        var environmentRecord = await environmentStore.GetByNameAsync(orgContext.OwnerKey, id, environmentName, ct);
        if (environmentRecord is null)
            return Results.NotFound();

        environmentBaseUrl = environmentRecord.BaseUrl;
    }

    if (!EnvironmentSelector.TryApplyBaseUrl(runtime, doc, environmentBaseUrl, out _, out var selectionError))
        return Results.Problem(title: "Base URL missing", detail: selectionError, statusCode: StatusCodes.Status409Conflict);

    var trimmedOperationId = operationId.Trim();
    TestPlan plan;

    var existingPlan = await planStore.GetAsync(id, trimmedOperationId, ct);
    if (existingPlan is null)
    {
        var match = FindOperation(doc, trimmedOperationId);
        if (match is null)
            return Results.NotFound();

        var (path, method, op) = match.Value;
        plan = TestPlanFactory.Create(op, method, path, trimmedOperationId);
        var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
        var redactedPlan = redactionService.RedactPlan(plan, org?.RedactionRules);
        var planJson = JsonSerializer.Serialize(redactedPlan, jsonOptions);
        await planStore.UpsertAsync(id, trimmedOperationId, planJson, DateTime.UtcNow, ct);
    }
    else
    {
        try
        {
            plan = JsonSerializer.Deserialize<TestPlan>(existingPlan.PlanJson, jsonOptions)
                ?? throw new JsonException("Stored test plan was empty.");
        }
        catch (JsonException)
        {
            return Results.Problem(title: "Stored test plan invalid", detail: "Stored test plan could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    logger.LogInformation("Executing run for project {ProjectId} operation {OperationId}", project.ProjectId, trimmedOperationId);
    var run = await runner.RunPlanAsync(plan, project.ProjectKey, tenantContext.TenantId, orgContext.OwnerKey, spec.SpecId, orgContext.OwnerKey, environmentName, ct);
    logger.LogInformation("Stored run {RunId} for project {ProjectId} operation {OperationId}", run.RunId, project.ProjectId, trimmedOperationId);
    RecordRunExecuted(httpContext, "projects.run.execute");

    var runMetadata = JsonSerializer.Serialize(new
    {
        project.ProjectId,
        project.ProjectKey,
        run.OperationId,
        Environment = environmentName
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.RunExecuted,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        runMetadata), ct);

    return Results.Ok(RunMapping.ToDetailDto(run));
});

v1.MapGet("/runs", async (string? projectKey, string? operationId, int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, ITestRunStore store, IProjectStore projectStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredKey(projectKey, "projectKey", out var keyError))
        return InvalidRequest(keyError);

    if (!RequestValidation.TryNormalizePageSize(pageSize, take, 20, 1, 200, out var normalizedPageSize, out var sizeError))
        return InvalidRequest(sizeError);

    if (!RequestValidation.TryNormalizePageToken(pageToken, skip, out var offset, out var tokenError))
        return InvalidRequest(tokenError);

    if (!RequestValidation.TryNormalizeOptionalValue(operationId, out var normalizedOperationId, out var opError))
        return InvalidRequest(opError);

    if (!RequestValidation.TryNormalizeSort(sort, SortField.StartedUtc, out var sortField, out var sortError))
        return InvalidRequest(sortError);

    if (!RequestValidation.TryNormalizeOrder(order, SortDirection.Desc, out var direction, out var orderError))
        return InvalidRequest(orderError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var retention = await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct);
    var project = await projectStore.GetByKeyAsync(tenantContext.TenantId, projectKey!.Trim(), ct);
    if (project is null)
        return Results.NotFound();

    logger.LogInformation(
        "Listing runs for project {ProjectKey} operation {OperationId}",
        projectKey!.Trim(),
        normalizedOperationId ?? "(all)");

    var result = await store.ListAsync(
        tenantContext.TenantId,
        projectKey!.Trim(),
        new PageRequest(normalizedPageSize, offset),
        sortField,
        direction,
        normalizedOperationId,
        retention.CutoffUtc);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(RunMapping.ToSummaryResponse(projectKey!.Trim(), metadata, result.Items));
});

v1.MapGet("/runs/{runId}", async (string runId, ITestRunStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    return run is null
        ? Results.NotFound()
        : ValidateRetentionOrResult(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct), RunMapping.ToDetailDto(run));
});

app.MapGet("/api/orgs/current", async (IOrganisationStore orgStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    return org is null
        ? Results.NotFound()
        : Results.Ok(OrgMapping.ToDto(org));
});

app.MapGet("/api/orgs/current/members", async (IOrganisationStore orgStore, IUserStore userStore, IMembershipStore membershipStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    if (!OrgRoleAccess.CanViewMembers(orgContext.Role))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    var memberships = await membershipStore.ListByOrganisationAsync(tenantContext.TenantId, ct);
    var members = new List<OrgMemberDto>(memberships.Count);

    foreach (var membership in memberships)
    {
        var user = await userStore.GetAsync(membership.UserId, ct);
        if (user is null)
            continue;

        members.Add(OrgMapping.ToMemberDto(user, membership.Role));
    }

    return Results.Ok(new OrgMembersResponse(members));
});

app.MapGet("/audit", async (int? take, string? action, string? from, string? to, IAuditEventStore auditStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var gate = await subscriptions.CheckTeamFeatureAccessAsync(tenantContext.TenantId, "Audit log", ct);
    if (!gate.Allowed)
        return SubscriptionProblem(gate);

    var normalizedTake = take ?? 50;
    if (normalizedTake is < 1 or > 200)
        return InvalidRequest("take must be between 1 and 200.");

    if (!TryParseAuditTimestamp(from, "from", out var fromUtc, out var fromError))
        return InvalidRequest(fromError);

    if (!TryParseAuditTimestamp(to, "to", out var toUtc, out var toError))
        return InvalidRequest(toError);

    if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        return InvalidRequest("from must be earlier than to.");

    var records = await auditStore.ListAsync(tenantContext.TenantId, normalizedTake, action, fromUtc, toUtc, ct);
    return Results.Ok(AuditMapping.ToListResponse(records));
});

app.MapPost("/admin/prune", async (IRetentionPruner pruner, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var result = await pruner.PruneAsync(tenantContext.TenantId, ct);
    return Results.Ok(new
    {
        result.OrganisationId,
        result.RetentionDays,
        result.CutoffUtc,
        result.RunsPruned
    });
});

app.MapPost("/api-keys", async (ApiKeyCreateRequest request, IApiTokenService apiTokenService, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, RequestValidation.MaxNameLength, out var nameError))
        return InvalidRequest(nameError);

    if (!ApiKeyScopes.TryNormalize(request?.Scopes, out var normalizedScopes, out var scopeError))
        return InvalidRequest(scopeError);

    if (request?.ExpiresUtc is { } expiresUtc && expiresUtc <= DateTime.UtcNow)
        return InvalidRequest("ExpiresUtc must be in the future.");

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var created = await apiTokenService.CreateTokenAsync(
        tenantContext.TenantId,
        orgContext.UserId,
        request!.Name!,
        normalizedScopes,
        request.ExpiresUtc,
        ct);
    var record = created.Record;

    var metadataJson = JsonSerializer.Serialize(new
    {
        record.Name,
        record.Scopes,
        record.ExpiresUtc
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ApiKeyCreated,
        "api_key",
        record.KeyId.ToString(),
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(new ApiKeyCreateResponse(ApiKeyMapping.ToDto(record), created.Token));
});

app.MapGet("/api-keys", async (IApiKeyStore apiKeyStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var keys = await apiKeyStore.ListAsync(tenantContext.TenantId, ct);
    return Results.Ok(ApiKeyMapping.ToListResponse(keys));
});

app.MapPost("/api-keys/{id}/revoke", async (string id, IApiKeyStore apiKeyStore, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(id, out var keyId, out var error))
        return InvalidRequest(httpContext, error);

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var record = await apiKeyStore.RevokeAsync(tenantContext.TenantId, keyId, DateTime.UtcNow, ct);
    if (record is null)
        return Results.NotFound();

    var metadataJson = JsonSerializer.Serialize(new
    {
        record.Name,
        record.RevokedUtc
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ApiKeyRevoked,
        "api_key",
        record.KeyId.ToString(),
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(ApiKeyMapping.ToDto(record));
});

app.MapGet("/api/projects", async (int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, IProjectStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryNormalizePageSize(pageSize, take, 50, 1, 200, out var normalizedPageSize, out var sizeError))
        return InvalidRequest(sizeError);

    if (!RequestValidation.TryNormalizePageToken(pageToken, skip, out var offset, out var tokenError))
        return InvalidRequest(tokenError);

    if (!RequestValidation.TryNormalizeSort(sort, SortField.CreatedUtc, out var sortField, out var sortError))
        return InvalidRequest(sortError);

    if (!RequestValidation.TryNormalizeOrder(order, SortDirection.Desc, out var direction, out var orderError))
        return InvalidRequest(orderError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var result = await store.ListAsync(tenantContext.TenantId, new PageRequest(normalizedPageSize, offset), sortField, direction, ct);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(ProjectMapping.ToListResponse(metadata, result.Items));
});

app.MapPost("/api/projects", async (ProjectCreateRequest request, IProjectStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, RequestValidation.MaxNameLength, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var orgContext = httpContext.GetOrgContext();
    var gate = await subscriptions.CheckProjectCreateAsync(tenantContext.TenantId, ct);
    if (!gate.Allowed)
        return SubscriptionProblem(gate);

    var project = await store.CreateAsync(tenantContext.TenantId, orgContext.OwnerKey, request!.Name!, ct);
    var totalProjects = await GetProjectCountAsync(store, tenantContext.TenantId, ct);
    await subscriptions.UpdateProjectsUsedAsync(tenantContext.TenantId, totalProjects, ct);
    logger.LogInformation("Created project {ProjectId} for org {OrganisationId} with name {ProjectName}", project.ProjectId, tenantContext.TenantId, project.Name);
    return Results.Ok(ProjectMapping.ToDto(project));
});

app.MapGet("/api/projects/{projectId}", async (string projectId, IProjectStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await store.GetAsync(tenantContext.TenantId, id, ct);
    return project is null
        ? await NotFoundOrForbiddenAsync(store, tenantContext, id, ct)
        : Results.Ok(ProjectMapping.ToDto(project));
});

app.MapGet("/api/projects/{projectId}/environments", async (string projectId, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var environments = await environmentStore.ListAsync(orgContext.OwnerKey, id, ct);
    return Results.Ok(EnvironmentMapping.ToListResponse(environments));
});

app.MapPost("/api/projects/{projectId}/environments", async (string projectId, EnvironmentCreateRequest request, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryValidateRequiredName(request?.Name, RequestValidation.MaxEnvironmentNameLength, out var nameError))
        return InvalidRequest(nameError);

    if (!RequestValidation.TryNormalizeBaseUrl(request?.BaseUrl, out var normalizedBaseUrl, out var baseUrlError))
        return InvalidRequest(baseUrlError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    try
    {
        var environment = await environmentStore.CreateAsync(orgContext.OwnerKey, id, request!.Name!, normalizedBaseUrl, ct);
        logger.LogInformation("Created environment {EnvironmentId} for project {ProjectId}", environment.EnvironmentId, id);
        return Results.Ok(EnvironmentMapping.ToDto(environment));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(title: "Invalid request", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
    }
});

app.MapGet("/api/projects/{projectId}/environments/{environmentId}", async (string projectId, string environmentId, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(environmentId, out var envId, out var envError))
        return InvalidRequest(envError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var environment = await environmentStore.GetAsync(orgContext.OwnerKey, id, envId, ct);
    return environment is null
        ? Results.NotFound()
        : Results.Ok(EnvironmentMapping.ToDto(environment));
});

app.MapPut("/api/projects/{projectId}/environments/{environmentId}", async (string projectId, string environmentId, EnvironmentUpdateRequest request, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(environmentId, out var envId, out var envError))
        return InvalidRequest(envError);

    if (!RequestValidation.TryValidateRequiredName(request?.Name, RequestValidation.MaxEnvironmentNameLength, out var nameError))
        return InvalidRequest(nameError);

    if (!RequestValidation.TryNormalizeBaseUrl(request?.BaseUrl, out var normalizedBaseUrl, out var baseUrlError))
        return InvalidRequest(baseUrlError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    try
    {
        var environment = await environmentStore.UpdateAsync(orgContext.OwnerKey, id, envId, request!.Name!, normalizedBaseUrl, ct);
        if (environment is null)
            return Results.NotFound();

        logger.LogInformation("Updated environment {EnvironmentId} for project {ProjectId}", envId, id);
        return Results.Ok(EnvironmentMapping.ToDto(environment));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(title: "Invalid request", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
    }
});

app.MapDelete("/api/projects/{projectId}/environments/{environmentId}", async (string projectId, string environmentId, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(environmentId, out var envId, out var envError))
        return InvalidRequest(envError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var removed = await environmentStore.DeleteAsync(orgContext.OwnerKey, id, envId, ct);
    if (!removed)
        return Results.NotFound();

    logger.LogInformation("Deleted environment {EnvironmentId} for project {ProjectId}", envId, id);
    return Results.NoContent();
});

app.MapPost("/api/projects/{projectId}/openapi/import", ImportOpenApiSpecAsync);
app.MapPost("/api/projects/{projectId}/specs/import", ImportOpenApiSpecAsync);

app.MapGet("/api/projects/{projectId}/openapi", async (string projectId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var record = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    return record is null
        ? Results.NotFound()
        : Results.Ok(OpenApiMapping.ToMetadataDto(record));
});

app.MapGet("/api/projects/{projectId}/specs", async (string projectId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var records = await specStore.ListAsync(tenantContext.TenantId, id, ct);
    return Results.Ok(records.Select(OpenApiMapping.ToMetadataDto));
});


app.MapGet("/api/projects/{projectId}/specs/{specId}", async (string projectId, string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(specId, out var specGuid, out var specError))
        return InvalidRequest(specError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var record = await specStore.GetByIdAsync(tenantContext.TenantId, specGuid, ct);
    if (record is null || record.ProjectId != id)
        return Results.NotFound();

    return Results.Ok(OpenApiMapping.ToMetadataDto(record));
});

app.MapGet("/api/projects/{projectId}/specs/diff", async (string projectId, string? from, string? to, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        return InvalidRequest("Both from and to spec ids are required.");

    if (!RequestValidation.TryParseGuid(from, out var specAId, out var specAError))
        return InvalidRequest(specAError);

    if (!RequestValidation.TryParseGuid(to, out var specBId, out var specBError))
        return InvalidRequest(specBError);

    var recordA = await specStore.GetByIdAsync(tenantContext.TenantId, specAId, ct);
    if (recordA is null || recordA.ProjectId != id)
        return Results.NotFound();

    var recordB = await specStore.GetByIdAsync(tenantContext.TenantId, specBId, ct);
    if (recordB is null || recordB.ProjectId != id)
        return Results.NotFound();

    var reader = new OpenApiStringReader();
    var docA = reader.Read(recordA.SpecJson, out _);
    if (docA is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Spec A could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var docB = reader.Read(recordB.SpecJson, out _);
    if (docB is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Spec B could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var diff = OpenApiDiffEngine.Diff(docA, docB);
    var response = new OpenApiDiffResponse(
        recordA.SpecId,
        recordB.SpecId,
        diff.Items.Select(item => new OpenApiDiffItemDto(
            item.Classification.ToString(),
            item.Change,
            item.Path,
            item.Method,
            item.Detail)).ToList());

    return Results.Ok(response);
});

app.MapGet("/api/specs/{specId}", async (string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(specId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var record = await specStore.GetByIdAsync(tenantContext.TenantId, id, ct);
    if (record is null)
        return Results.NotFound();

    var project = await projectStore.GetAsync(tenantContext.TenantId, record.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, record.ProjectId, ct);

    return Results.Ok(OpenApiMapping.ToDetailDto(record));
});

app.MapGet("/specs/{specA}/diff/{specB}", async (string specA, string specB, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(specA, out var specAId, out var specAError))
        return InvalidRequest(specAError);

    if (!RequestValidation.TryParseGuid(specB, out var specBId, out var specBError))
        return InvalidRequest(specBError);

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();

    var recordA = await specStore.GetByIdAsync(tenantContext.TenantId, specAId, ct);
    if (recordA is null)
        return Results.NotFound();

    var projectA = await projectStore.GetAsync(tenantContext.TenantId, recordA.ProjectId, ct);
    if (projectA is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, recordA.ProjectId, ct);

    var recordB = await specStore.GetByIdAsync(tenantContext.TenantId, specBId, ct);
    if (recordB is null)
        return Results.NotFound();

    var projectB = await projectStore.GetAsync(tenantContext.TenantId, recordB.ProjectId, ct);
    if (projectB is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, recordB.ProjectId, ct);

    var reader = new OpenApiStringReader();
    var docA = reader.Read(recordA.SpecJson, out _);
    if (docA is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Spec A could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var docB = reader.Read(recordB.SpecJson, out _);
    if (docB is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Spec B could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var diff = OpenApiDiffEngine.Diff(docA, docB);
    var response = new OpenApiDiffResponse(
        recordA.SpecId,
        recordB.SpecId,
        diff.Items.Select(item => new OpenApiDiffItemDto(
            item.Classification.ToString(),
            item.Change,
            item.Path,
            item.Method,
            item.Detail)).ToList());

    return Results.Ok(response);
});

app.MapPost("/api/projects/{projectId}/testplans/{operationId}/generate", async (
    string projectId,
    string operationId,
    IProjectStore projectStore,
    IOrganisationStore orgStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    RedactionService redactionService,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before generating a test plan.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var match = FindOperation(doc, operationId);
    if (match is null)
        return Results.NotFound();

    var (path, method, op) = match.Value;
    var plan = TestPlanFactory.Create(op, method, path, operationId.Trim());
    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var redactedPlan = redactionService.RedactPlan(plan, org?.RedactionRules);
    var planJson = JsonSerializer.Serialize(redactedPlan, jsonOptions);

    var record = await planStore.UpsertAsync(id, operationId.Trim(), planJson, DateTime.UtcNow, ct);
    return Results.Ok(new TestPlanResponse(record.ProjectId, record.OperationId, record.PlanJson, record.CreatedUtc));
});

app.MapGet("/api/projects/{projectId}/testplans/{operationId}", async (
    string projectId,
    string operationId,
    IProjectStore projectStore,
    ITestPlanStore planStore,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var record = await planStore.GetAsync(id, operationId.Trim(), ct);
    return record is null
        ? Results.NotFound()
        : Results.Ok(new TestPlanResponse(record.ProjectId, record.OperationId, record.PlanJson, record.CreatedUtc));
});

app.MapPost("/api/projects/{projectId}/runs/execute/{operationId}", async (
    string projectId,
    string operationId,
    string? environment,
    IProjectStore projectStore,
    IOrganisationStore orgStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    IEnvironmentStore environmentStore,
    SubscriptionEnforcementService subscriptions,
    TestPlanRunner runner,
    ApiRuntimeConfig runtime,
    RedactionService redactionService,
    IAuditEventStore auditStore,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var runGate = await subscriptions.TryConsumeRunAsync(tenantContext.TenantId, ct);
    if (!runGate.Allowed)
        return SubscriptionProblem(runGate);

    var spec = await specStore.GetAsync(tenantContext.TenantId, id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before executing runs.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    if (!RequestValidation.TryNormalizeOptionalValue(environment, out var environmentName, out var environmentError))
        return InvalidRequest(environmentError);

    string? environmentBaseUrl = null;
    if (!string.IsNullOrWhiteSpace(environmentName))
    {
        var environmentRecord = await environmentStore.GetByNameAsync(orgContext.OwnerKey, id, environmentName, ct);
        if (environmentRecord is null)
            return Results.NotFound();

        environmentBaseUrl = environmentRecord.BaseUrl;
    }

    if (!EnvironmentSelector.TryApplyBaseUrl(runtime, doc, environmentBaseUrl, out _, out var selectionError))
        return Results.Problem(title: "Base URL missing", detail: selectionError, statusCode: StatusCodes.Status409Conflict);

    var trimmedOperationId = operationId.Trim();
    TestPlan plan;

    var existingPlan = await planStore.GetAsync(id, trimmedOperationId, ct);
    if (existingPlan is null)
    {
        var match = FindOperation(doc, trimmedOperationId);
        if (match is null)
            return Results.NotFound();

        var (path, method, op) = match.Value;
        plan = TestPlanFactory.Create(op, method, path, trimmedOperationId);
        var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
        var redactedPlan = redactionService.RedactPlan(plan, org?.RedactionRules);
        var planJson = JsonSerializer.Serialize(redactedPlan, jsonOptions);
        await planStore.UpsertAsync(id, trimmedOperationId, planJson, DateTime.UtcNow, ct);
    }
    else
    {
        try
        {
            plan = JsonSerializer.Deserialize<TestPlan>(existingPlan.PlanJson, jsonOptions)
                ?? throw new JsonException("Stored test plan was empty.");
        }
        catch (JsonException)
        {
            return Results.Problem(title: "Stored test plan invalid", detail: "Stored test plan could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    logger.LogInformation("Executing run for project {ProjectId} operation {OperationId}", project.ProjectId, trimmedOperationId);
    var run = await runner.RunPlanAsync(plan, project.ProjectKey, tenantContext.TenantId, orgContext.OwnerKey, spec.SpecId, orgContext.OwnerKey, environmentName, ct);
    logger.LogInformation("Stored run {RunId} for project {ProjectId} operation {OperationId}", run.RunId, project.ProjectId, trimmedOperationId);
    RecordRunExecuted(httpContext, "projects.run.execute");

    var runMetadata = JsonSerializer.Serialize(new
    {
        project.ProjectId,
        project.ProjectKey,
        run.OperationId,
        Environment = environmentName
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.RunExecuted,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        runMetadata), ct);

    return Results.Ok(RunMapping.ToDetailDto(run));
});

app.MapPost("/test-plans/from-ai-draft/{draftId}/run", RunDraftPlanFromAiAsync);
app.MapPost("/api/test-plans/from-ai-draft/{draftId}/run", RunDraftPlanFromAiAsync);

app.MapGet("/api/runs", async (string? projectKey, string? operationId, int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, ITestRunStore store, IProjectStore projectStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredKey(projectKey, "projectKey", out var keyError))
        return InvalidRequest(keyError);

    if (!RequestValidation.TryNormalizePageSize(pageSize, take, 20, 1, 200, out var normalizedPageSize, out var sizeError))
        return InvalidRequest(sizeError);

    if (!RequestValidation.TryNormalizePageToken(pageToken, skip, out var offset, out var tokenError))
        return InvalidRequest(tokenError);

    if (!RequestValidation.TryNormalizeOptionalValue(operationId, out var normalizedOperationId, out var opError))
        return InvalidRequest(opError);

    if (!RequestValidation.TryNormalizeSort(sort, SortField.StartedUtc, out var sortField, out var sortError))
        return InvalidRequest(sortError);

    if (!RequestValidation.TryNormalizeOrder(order, SortDirection.Desc, out var direction, out var orderError))
        return InvalidRequest(orderError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var retention = await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct);
    var project = await projectStore.GetByKeyAsync(tenantContext.TenantId, projectKey!.Trim(), ct);
    if (project is null)
        return Results.NotFound();

    logger.LogInformation(
        "Listing runs for project {ProjectKey} operation {OperationId}",
        projectKey!.Trim(),
        normalizedOperationId ?? "(all)");

    var result = await store.ListAsync(
        tenantContext.TenantId,
        projectKey!.Trim(),
        new PageRequest(normalizedPageSize, offset),
        sortField,
        direction,
        normalizedOperationId,
        retention.CutoffUtc);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(RunMapping.ToSummaryResponse(projectKey!.Trim(), metadata, result.Items));
});

app.MapGet("/api/runs/{runId}", async (string runId, ITestRunStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    return run is null
        ? Results.NotFound()
        : ValidateRetentionOrResult(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct), RunMapping.ToDetailDto(run));
});

app.MapGet("/api/runs/{runId}/summary", async (string runId, ITestRunStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    return Results.Ok(RunMapping.ToSummaryCounts(run));
});

app.MapGet("/api/runs/{runId}/audit", async (string runId, ITestRunStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    return Results.Ok(RunMapping.ToAuditResponse(run));
});

app.MapGet("/api/runs/{runId}/compliance-report", async (string runId, ITestRunStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    return Results.Ok(RunMapping.ToComplianceReport(run));
});

app.MapGet("/api/runs/{runId}/annotations", async (string runId, ITestRunStore runStore, IRunAnnotationStore annotationStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var annotations = await annotationStore.ListAsync(orgContext.OwnerKey, id, ct);
    return Results.Ok(RunAnnotationMapping.ToListResponse(id, annotations));
});

app.MapPost("/api/runs/{runId}/annotations", async (string runId, RunAnnotationCreateRequest request, ITestRunStore runStore, IRunAnnotationStore annotationStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryNormalizeAnnotationNote(request?.Note, out var normalizedNote, out var noteError))
        return InvalidRequest(noteError);

    if (!RequestValidation.TryNormalizeOptionalJiraLink(request?.JiraLink, out var normalizedJiraLink, out var jiraError))
        return InvalidRequest(jiraError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var annotation = await annotationStore.CreateAsync(orgContext.OwnerKey, id, normalizedNote, normalizedJiraLink, ct);
    logger.LogInformation("Created annotation {AnnotationId} for run {RunId}", annotation.AnnotationId, id);
    return Results.Ok(RunAnnotationMapping.ToDto(annotation));
});

app.MapGet("/api/runs/{runId}/annotations/{annotationId}", async (string runId, string annotationId, ITestRunStore runStore, IRunAnnotationStore annotationStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(annotationId, out var annotationGuid, out var annotationError))
        return InvalidRequest(annotationError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var annotation = await annotationStore.GetAsync(orgContext.OwnerKey, id, annotationGuid, ct);
    return annotation is null
        ? Results.NotFound()
        : Results.Ok(RunAnnotationMapping.ToDto(annotation));
});

app.MapPut("/api/runs/{runId}/annotations/{annotationId}", async (string runId, string annotationId, RunAnnotationUpdateRequest request, ITestRunStore runStore, IRunAnnotationStore annotationStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(annotationId, out var annotationGuid, out var annotationError))
        return InvalidRequest(annotationError);

    if (!RequestValidation.TryNormalizeAnnotationNote(request?.Note, out var normalizedNote, out var noteError))
        return InvalidRequest(noteError);

    if (!RequestValidation.TryNormalizeOptionalJiraLink(request?.JiraLink, out var normalizedJiraLink, out var jiraError))
        return InvalidRequest(jiraError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var annotation = await annotationStore.UpdateAsync(orgContext.OwnerKey, id, annotationGuid, normalizedNote, normalizedJiraLink, ct);
    if (annotation is null)
        return Results.NotFound();

    logger.LogInformation("Updated annotation {AnnotationId} for run {RunId}", annotationGuid, id);
    return Results.Ok(RunAnnotationMapping.ToDto(annotation));
});

app.MapDelete("/api/runs/{runId}/annotations/{annotationId}", async (string runId, string annotationId, ITestRunStore runStore, IRunAnnotationStore annotationStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!RequestValidation.TryParseGuid(annotationId, out var annotationGuid, out var annotationError))
        return InvalidRequest(annotationError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var removed = await annotationStore.DeleteAsync(orgContext.OwnerKey, id, annotationGuid, ct);
    if (!removed)
        return Results.NotFound();

    logger.LogInformation("Deleted annotation {AnnotationId} for run {RunId}", annotationGuid, id);
    return Results.NoContent();
});

app.MapGet("/api/runs/{runId}/report", async (string runId, string? format, ITestRunStore store, SubscriptionEnforcementService subscriptions, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    if (!TryParseReportFormat(format, out var reportFormat, out var formatError))
        return InvalidRequest(formatError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var exportGate = await subscriptions.TryConsumeExportAsync(tenantContext.TenantId, ct);
    if (!exportGate.Allowed)
        return SubscriptionProblem(httpContext, exportGate);

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var report = RunReportGenerator.Generate(run, reportFormat);
    var contentType = reportFormat == RunReportFormat.Markdown
        ? "text/markdown; charset=utf-8"
        : "text/html; charset=utf-8";

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = reportFormat.ToString()
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    RecordExportGenerated(httpContext, reportFormat.ToString().ToLowerInvariant());
    return Results.Text(report, contentType);
});

app.MapGet("/runs/{runId}/export/junit", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, SubscriptionEnforcementService subscriptions, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var exportGate = await subscriptions.TryConsumeExportAsync(tenantContext.TenantId, ct);
    if (!exportGate.Allowed)
        return SubscriptionProblem(httpContext, exportGate);

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var content = RunExportGenerator.GenerateJunit(redactedRun);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "junit"
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    RecordExportGenerated(httpContext, "junit");
    return Results.Text(content, "application/junit+xml; charset=utf-8");
});

app.MapGet("/runs/{runId}/export/json", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, SubscriptionEnforcementService subscriptions, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var exportGate = await subscriptions.TryConsumeExportAsync(tenantContext.TenantId, ct);
    if (!exportGate.Allowed)
        return SubscriptionProblem(httpContext, exportGate);

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var payload = RunExportGenerator.GenerateJson(redactedRun, jsonOptions);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "json"
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    RecordExportGenerated(httpContext, "json");
    return Results.Text(payload, "application/json; charset=utf-8");
});

app.MapGet("/runs/{runId}/export/csv", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, SubscriptionEnforcementService subscriptions, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var exportGate = await subscriptions.TryConsumeExportAsync(tenantContext.TenantId, ct);
    if (!exportGate.Allowed)
        return SubscriptionProblem(httpContext, exportGate);

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var payload = RunExportGenerator.GenerateCsv(redactedRun);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "csv"
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    RecordExportGenerated(httpContext, "csv");
    return Results.Text(payload, "text/csv; charset=utf-8");
});

app.MapGet("/runs/{runId}/export/evidence-bundle", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, IAuditEventStore auditStore, SubscriptionEnforcementService subscriptions, IConfiguration configuration, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var exportGate = await subscriptions.CheckTeamFeatureAccessAsync(tenantContext.TenantId, "Evidence pack export", ct);
    if (!exportGate.Allowed)
        return SubscriptionProblem(httpContext, exportGate);

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var auditEvents = await auditStore.ListAsync(tenantContext.TenantId, 200, null, null, null, ct);
    var auditSubset = auditEvents
        .Where(evt => string.Equals(evt.TargetType, "run", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(evt.TargetId, run.RunId.ToString(), StringComparison.OrdinalIgnoreCase))
        .Take(100)
        .ToList();
    var evidencePackBytes = EvidencePackBuilder.BuildZip(
        run,
        org,
        auditSubset,
        redactionService,
        jsonOptions,
        DateTime.UtcNow,
        configuration["Security:EvidenceSigningKey"]);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "evidence-bundle"
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    RecordExportGenerated(httpContext, "evidence-bundle");
    return Results.File(evidencePackBytes, "application/zip", $"{run.RunId}-evidence.zip");
});

app.MapGet("/api/v1/runs/{runId}/evidence-pack", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, IAuditEventStore auditStore, SubscriptionEnforcementService subscriptions, IConfiguration configuration, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var exportGate = await subscriptions.CheckTeamFeatureAccessAsync(tenantContext.TenantId, "Evidence pack export", ct);
    if (!exportGate.Allowed)
        return SubscriptionProblem(httpContext, exportGate);

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var auditEvents = await auditStore.ListAsync(tenantContext.TenantId, 200, null, null, null, ct);
    var auditSubset = auditEvents
        .Where(evt => string.Equals(evt.TargetType, "run", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(evt.TargetId, run.RunId.ToString(), StringComparison.OrdinalIgnoreCase))
        .Take(100)
        .ToList();

    var auditJson = JsonSerializer.Serialize(new
    {
        redactedRun.RunId,
        Events = auditSubset
    }, jsonOptions);
    var junitExport = RunExportGenerator.GenerateJunit(redactedRun);
    var csvExport = RunExportGenerator.GenerateCsv(redactedRun);
    var complianceReport = ComplianceReportBuilder.Build(run, org, auditSubset.Take(25).ToList(), redactionService);
    var complianceReportJson = JsonSerializer.Serialize(complianceReport, jsonOptions);

    var manifest = JsonSerializer.Serialize(new
    {
        generatedUtc = DateTime.UtcNow,
        runId = redactedRun.RunId,
        entries = new[]
        {
            "manifest.json",
            "run.json",
            "policy.json",
            "audit.json",
            "compliance-report.json",
            "exports/junit.xml",
            "exports/results.csv"
        }
    }, jsonOptions);

    using var stream = new MemoryStream();
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
    {
        AddZipEntry(archive, "manifest.json", manifest);
        AddZipEntry(archive, "run.json", runJson);
        AddZipEntry(archive, "policy.json", policyJson);
        AddZipEntry(archive, "audit.json", auditJson);
        AddZipEntry(archive, "compliance-report.json", complianceReportJson);
        AddZipEntry(archive, "exports/junit.xml", junitExport);
        AddZipEntry(archive, "exports/results.csv", csvExport);
    }
    var evidencePackBytes = EvidencePackBuilder.BuildZip(
        run,
        org,
        auditSubset,
        redactionService,
        jsonOptions,
        DateTime.UtcNow,
        configuration["Security:EvidenceSigningKey"]);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "evidence-pack"
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    RecordExportGenerated(httpContext, "evidence-pack");
    return Results.File(evidencePackBytes, "application/zip", $"{run.RunId}-evidence-pack.zip");
});

app.MapGet("/api/v1/runs/{runId}/audit", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, IAuditEventStore auditStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    var auditEvents = await auditStore.ListAsync(tenantContext.TenantId, 200, null, null, null, ct);
    var auditSubset = auditEvents
        .Where(evt => string.Equals(evt.TargetType, "run", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(evt.TargetId, run.RunId.ToString(), StringComparison.OrdinalIgnoreCase))
        .Take(100)
        .ToList();

    var response = EvidencePackBuilder.BuildImmutableAudit(
        run,
        org,
        auditSubset,
        redactionService,
        jsonOptions,
        DateTime.UtcNow);

    return Results.Ok(response);
});

app.MapPost("/api/runs/{runId}/baseline/{baselineRunId}", async (string runId, string baselineRunId, ITestRunStore store, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    if (!RequestValidation.TryParseGuid(baselineRunId, out var baselineId, out var baselineError))
        return InvalidRequest(baselineError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var updated = await store.SetBaselineAsync(tenantContext.TenantId, id, baselineId);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/baselines", async (BaselineCreateRequest request, ITestRunStore store, IBaselineStore baselineStore, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (request is null || request.RunId == Guid.Empty)
        return InvalidRequest("runId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, request.RunId);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var baseline = await baselineStore.SetAsync(tenantContext.TenantId, run.ProjectKey, run.OperationId, run.RunId, ct);
    if (baseline is null)
        return Results.NotFound();

    return Results.Ok(new BaselineDto(baseline.RunId, baseline.ProjectKey, baseline.OperationId, baseline.SetUtc));
});

app.MapGet("/api/baselines", async (string? projectKey, string? operationId, int? take, IBaselineStore baselineStore, HttpContext httpContext, CancellationToken ct) =>
{
    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    if (take.HasValue && (take.Value <= 0 || take.Value > 500))
        return InvalidRequest("take must be between 1 and 500.");

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var baselines = await baselineStore.ListAsync(tenantContext.TenantId, projectKey, operationId, take ?? 50, ct);
    var response = baselines
        .Select(baseline => new BaselineDto(baseline.RunId, baseline.ProjectKey, baseline.OperationId, baseline.SetUtc))
        .ToList();

    return Results.Ok(new BaselineListResponse(response));
});

app.MapGet("/api/runs/{runId}/compare/{baselineRunId}", async (string runId, string baselineRunId, ITestRunStore store, RunComparisonService comparison, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    if (!RequestValidation.TryParseGuid(baselineRunId, out var baselineId, out var baselineError))
        return InvalidRequest(baselineError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var retention = await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct);
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var runRetention = ValidateRetention(httpContext, run, retention);
    if (runRetention is not null)
        return runRetention;

    var baseline = await store.GetAsync(tenantContext.TenantId, baselineId);
    if (baseline is null)
        return Results.NotFound();

    var baselineRetention = ValidateRetention(baseline, retention);
    if (baselineRetention is not null)
        return baselineRetention;

    var response = comparison.Compare(run, baseline);
    return Results.Ok(response);
});

app.MapGet("/api/runs/{runId}/compare-to-baseline", async (string runId, ITestRunStore store, BaselineComparisonService comparison, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var result = await comparison.CompareAsync(tenantContext.TenantId, id, ct);

    return result.Status switch
    {
        BaselineComparisonStatus.Ok => Results.Ok(result.Response),
        BaselineComparisonStatus.RunNotFound => Results.NotFound(),
        _ => Results.NotFound()
    };
});

app.MapPost("/api/ai/runs/{runId}/explanation", async (string runId, ITestRunStore store, IAiClient aiClient, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await store.GetAsync(tenantContext.TenantId, id);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "run.explanation", ct);

    var context = AiContextFactory.BuildRunExplanationContext(run);
    var runJson = JsonSerializer.Serialize(context, jsonOptions);
    var prompt = AiPromptTemplates.BuildRunExplanationPrompt(runJson);
    var aiResponse = await aiClient.GetResponseAsync(prompt, ct);
    return Results.Ok(new AiRunExplanationResponse(run.RunId, aiResponse.Content));
});

async Task<IResult> ExplainAiEndpointAsync(
    HttpContext httpContext,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    IOrganisationStore orgStore,
    AiExplainService aiExplainService,
    SubscriptionEnforcementService subscriptions,
    CancellationToken ct)
{
    var payload = await httpContext.Request.ReadFromJsonAsync<AiExplainRequest>(cancellationToken: ct);
    if (payload is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryParseGuid(payload.ProjectId, out var projectId, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(payload.OperationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "explain", ct);

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, projectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, projectId, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, projectId, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before generating an explanation.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var trimmedOperationId = payload.OperationId.Trim();
    var match = FindOperation(doc, trimmedOperationId);
    if (match is null)
        return Results.NotFound();

    var (path, method, op) = match.Value;
    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    try
    {
        var input = new AiExplainInput(org, projectId, trimmedOperationId, method.ToString(), path, doc, op);
        var result = await aiExplainService.ExplainAsync(input, ct);
        ApplyFreePreviewWatermarkIfNeeded(httpContext, await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct));
        var response = new AiExplainResponse(
            projectId,
            trimmedOperationId,
            result.Payload.Summary,
            result.Payload.Inputs,
            result.Payload.Outputs,
            result.Payload.Auth,
            result.Payload.Gotchas,
            result.Payload.Examples.Select(example => new AiExplainExampleDto(example.Title, example.Content)).ToList(),
            result.Payload.Markdown);
        return Results.Ok(response);
    }
    catch (AiSchemaValidationException ex)
    {
        return Results.Problem(title: "AI response invalid", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}

async Task<IResult> SuggestAiTestsEndpointAsync(
    HttpContext httpContext,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    IOrganisationStore orgStore,
    AiSuggestTestsService aiSuggestTestsService,
    SubscriptionEnforcementService subscriptions,
    CancellationToken ct)
{
    var payload = await httpContext.Request.ReadFromJsonAsync<AiSuggestTestsRequest>(cancellationToken: ct);
    if (payload is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryParseGuid(payload.ProjectId, out var projectId, out var error))
        return InvalidRequest(httpContext, error);

    if (string.IsNullOrWhiteSpace(payload.OperationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "suggest-tests", ct);

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, projectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, projectId, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, projectId, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before requesting AI suggestions.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var trimmedOperationId = payload.OperationId.Trim();
    var match = FindOperation(doc, trimmedOperationId);
    if (match is null)
        return Results.NotFound();

    var (path, method, op) = match.Value;
    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    try
    {
        var input = new AiSuggestTestsInput(org, projectId, trimmedOperationId, method.ToString(), path, op);
        var result = await aiSuggestTestsService.SuggestAsync(input, ct);
        ApplyFreePreviewWatermarkIfNeeded(httpContext, await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct));
        return Results.Ok(new ApiTester.Web.Contracts.AiSuggestTestsResponse(
            result.Draft.DraftId,
            result.Draft.ProjectId,
            result.Draft.OperationId,
            result.Draft.PlanJson,
            result.Draft.CreatedUtc));
    }
    catch (AiSchemaValidationException ex)
    {
        return Results.Problem(title: "AI response invalid", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (AiFeatureDisabledException ex)
    {
        return Results.Problem(title: "Feature not available", detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (AiRateLimitExceededException ex)
    {
        return Results.Problem(title: "AI rate limit exceeded", detail: ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
    }
}

async Task<IResult> SummariseRunAiEndpointAsync(
    HttpContext httpContext,
    ITestRunStore runStore,
    IProjectStore projectStore,
    IOrganisationStore orgStore,
    AiRunSummaryService summaryService,
    SubscriptionEnforcementService subscriptions,
    CancellationToken ct)
{
    var payload = await httpContext.Request.ReadFromJsonAsync<AiSummariseRunRequest>(cancellationToken: ct);
    if (payload is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryParseGuid(payload.RunId, out var runId, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, runId);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "summarise-run", ct);

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetByKeyAsync(tenantContext.TenantId, run.ProjectKey, ct);
    if (project is null)
        return Results.NotFound();

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    try
    {
        var input = new AiRunSummaryInput(org, project.ProjectId, run);
        var result = await summaryService.SummariseAsync(input, ct);
        ApplyFreePreviewWatermarkIfNeeded(httpContext, await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct));
        var response = new AiRunSummaryResponse(
            run.RunId,
            result.Payload.OverallSummary,
            result.Payload.TopFailures.Select(failure => new AiRunSummaryFailureDto(
                failure.Title,
                failure.EvidenceRefs.Select(evidence => new AiRunSummaryEvidenceRefDto(
                    evidence.CaseName,
                    evidence.FailureReason)).ToList())).ToList(),
            result.Payload.FlakeAssessment,
            new AiRunSummaryRegressionLikelihoodDto(
                result.Payload.RegressionLikelihood.Level,
                result.Payload.RegressionLikelihood.Rationale),
            result.Payload.RecommendedNextActions.ToList());

        return Results.Ok(response);
    }
    catch (AiSchemaValidationException ex)
    {
        return Results.Problem(title: "AI response invalid", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (AiFeatureDisabledException ex)
    {
        return Results.Problem(title: "Feature not available", detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (AiRateLimitExceededException ex)
    {
        return Results.Problem(title: "AI rate limit exceeded", detail: ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
    }
}

async Task<IResult> SuggestImprovementsAiEndpointAsync(
    HttpContext httpContext,
    ITestRunStore runStore,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    IOrganisationStore orgStore,
    AiAnalysisService analysisService,
    SubscriptionEnforcementService subscriptions,
    CancellationToken ct)
{
    var payload = await httpContext.Request.ReadFromJsonAsync<AiSuggestImprovementsRequest>(cancellationToken: ct);
    if (payload is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryParseGuid(payload.RunId, out var runId, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, runId);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "suggest-improvements", ct);

    var project = await projectStore.GetByKeyAsync(tenantContext.TenantId, run.ProjectKey, ct);
    if (project is null)
        return Results.NotFound();

    var spec = await specStore.GetAsync(tenantContext.TenantId, project.ProjectId, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before requesting AI suggestions.", statusCode: StatusCodes.Status409Conflict);

    var doc = new OpenApiStringReader().Read(spec.SpecJson, out _);
    var match = doc is null ? null : FindOperation(doc, run.OperationId);
    if (match is null)
        return Results.NotFound();

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    try
    {
        var (path, method, operation) = match.Value;
        var policy = run.PolicySnapshot ?? new ApiExecutionPolicySnapshot(
            false,
            false,
            [],
            [],
            true,
            true,
            30,
            262_144,
            262_144,
            false,
            false,
            0);
        var input = new AiAnalysisInput(org, project.ProjectId, run.OperationId, method.ToString(), path, operation, policy, run);
        var insights = await analysisService.AnalyzeAsync(input, ct);

        ApplyFreePreviewWatermarkIfNeeded(httpContext, await subscriptions.GetSnapshotAsync(tenantContext.TenantId, ct));

        return Results.Ok(new AiSuggestImprovementsResponse(
            run.RunId,
            insights.Select(i => new AiImprovementSuggestionDto(i.Type, i.JsonPayload)).ToList()));
    }
    catch (AiSchemaValidationException ex)
    {
        return Results.Problem(title: "AI response invalid", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}

async Task<IResult> ComplianceReportAiEndpointAsync(
    HttpContext httpContext,
    ITestRunStore runStore,
    IOrganisationStore orgStore,
    IAuditEventStore auditStore,
    RedactionService redactionService,
    IAiClient aiClient,
    SubscriptionEnforcementService subscriptions,
    CancellationToken ct)
{
    var payload = await httpContext.Request.ReadFromJsonAsync<AiComplianceReportRequest>(cancellationToken: ct);
    if (payload is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryParseGuid(payload.RunId, out var runId, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var run = await runStore.GetAsync(tenantContext.TenantId, runId);
    if (run is null)
        return Results.NotFound();

    var retentionResult = ValidateRetention(run, await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct));
    if (retentionResult is not null)
        return retentionResult;

    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "compliance-report", ct);

    var orgContext = httpContext.GetOrgContext();
    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    var auditEvents = await auditStore.ListAsync(tenantContext.TenantId, 200, null, null, null, ct);
    var auditSubset = auditEvents
        .Where(evt => string.Equals(evt.TargetType, "run", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(evt.TargetId, run.RunId.ToString(), StringComparison.OrdinalIgnoreCase))
        .Take(25)
        .ToList();

    var report = ComplianceReportBuilder.Build(run, org, auditSubset, redactionService);
    var reportJson = JsonSerializer.Serialize(report, jsonOptions);
    var prompt = AiPromptTemplates.BuildComplianceReportPrompt(reportJson);
    var aiResponse = await aiClient.GetResponseAsync(prompt, ct);

    return Results.Ok(report with { Narrative = aiResponse.Content });
}

async Task<IResult> GenerateDocsAiEndpointAsync(
    HttpContext httpContext,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    ITestRunStore runStore,
    IOrganisationStore orgStore,
    IGeneratedDocsStore docsStore,
    AiDocsGenerationService docsService,
    SubscriptionEnforcementService subscriptions,
    CancellationToken ct)
{
    var payload = await httpContext.Request.ReadFromJsonAsync<AiGenerateDocsRequest>(cancellationToken: ct);
    if (payload is null)
        return InvalidRequest("Request body is required.");

    if (!RequestValidation.TryParseGuid(payload.ProjectId, out var projectId, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "docs.generate", ct);

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, projectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, projectId, ct);

    var spec = await specStore.GetAsync(tenantContext.TenantId, projectId, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before generating docs.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    var operations = doc.Paths?
        .SelectMany(path => path.Value.Operations.Values)
        .Count(operation => !string.IsNullOrWhiteSpace(operation.OperationId)) ?? 0;
    if (operations == 0)
        return Results.Problem(title: "OpenAPI operations missing", detail: "OpenAPI spec must include operations with operationId values.", statusCode: StatusCodes.Status409Conflict);

    var org = await orgStore.GetAsync(tenantContext.TenantId, ct);
    if (org is null)
        return Results.NotFound();

    try
    {
        var retention = await subscriptions.GetRetentionWindowAsync(tenantContext.TenantId, ct);
        var runs = await runStore.ListAsync(
            tenantContext.TenantId,
            project.ProjectKey,
            new PageRequest(100, 0),
            SortField.StartedUtc,
            SortDirection.Desc,
            null,
            retention.CutoffUtc);

        var runDetails = new List<TestRunRecord>();
        foreach (var run in runs.Items)
        {
            var detailed = await runStore.GetAsync(tenantContext.TenantId, run.RunId);
            if (detailed is not null)
                runDetails.Add(detailed);
        }

        var input = new AiDocsGenerationInput(org, project, spec, doc, runDetails);
        var result = await docsService.GenerateAsync(input, ct);
        var record = await docsStore.UpsertAsync(
            tenantContext.TenantId,
            project.ProjectId,
            spec.SpecId,
            result.RawResponse,
            result.CreatedUtc,
            ct);

        var response = GeneratedDocsMapping.ToResponse(record, result.Payload);
        return Results.Ok(response);
    }
    catch (AiSchemaValidationException ex)
    {
        return Results.Problem(title: "AI response invalid", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (AiFeatureDisabledException ex)
    {
        return Results.Problem(title: "Feature not available", detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (AiRateLimitExceededException ex)
    {
        return Results.Problem(title: "AI rate limit exceeded", detail: ex.Message, statusCode: StatusCodes.Status429TooManyRequests);
    }
}

async Task<IResult> GetGeneratedDocsAsync(
    string projectId,
    IProjectStore projectStore,
    IGeneratedDocsStore docsStore,
    HttpContext httpContext,
    CancellationToken ct)
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    var record = await docsStore.GetAsync(tenantContext.TenantId, id, ct);
    if (record is null)
        return Results.NotFound();

    try
    {
        var payload = AiDocsSchemas.ParseDocs(record.DocsJson);
        var response = GeneratedDocsMapping.ToResponse(record, payload);
        return Results.Ok(response);
    }
    catch (AiSchemaValidationException ex)
    {
        return Results.Problem(title: "Stored docs invalid", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
}

async Task<IResult> RunDraftPlanFromAiAsync(
    string draftId,
    string? environment,
    ITestPlanDraftStore draftStore,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    IEnvironmentStore environmentStore,
    SubscriptionEnforcementService subscriptions,
    TestPlanRunner runner,
    ApiRuntimeConfig runtime,
    IAuditEventStore auditStore,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct)
{
    if (!RequestValidation.TryParseGuid(draftId, out var draftGuid, out var draftError))
        return InvalidRequest(draftError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var draft = await draftStore.GetAsync(draftGuid, ct);
    if (draft is null)
        return Results.NotFound();

    var project = await projectStore.GetAsync(tenantContext.TenantId, draft.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, draft.ProjectId, ct);

    var runGate = await subscriptions.TryConsumeRunAsync(tenantContext.TenantId, ct);
    if (!runGate.Allowed)
        return SubscriptionProblem(runGate);

    var spec = await specStore.GetAsync(tenantContext.TenantId, draft.ProjectId, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before executing runs.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    if (!RequestValidation.TryNormalizeOptionalValue(environment, out var environmentName, out var environmentError))
        return InvalidRequest(environmentError);

    string? environmentBaseUrl = null;
    if (!string.IsNullOrWhiteSpace(environmentName))
    {
        var environmentRecord = await environmentStore.GetByNameAsync(orgContext.OwnerKey, draft.ProjectId, environmentName, ct);
        if (environmentRecord is null)
            return Results.NotFound();

        environmentBaseUrl = environmentRecord.BaseUrl;
    }

    if (!EnvironmentSelector.TryApplyBaseUrl(runtime, doc, environmentBaseUrl, out _, out var selectionError))
        return Results.Problem(title: "Base URL missing", detail: selectionError, statusCode: StatusCodes.Status409Conflict);

    TestPlan plan;
    try
    {
        plan = JsonSerializer.Deserialize<TestPlan>(draft.PlanJson, jsonOptions)
            ?? throw new JsonException("Stored draft plan was empty.");
    }
    catch (JsonException)
    {
        return Results.Problem(title: "Stored draft plan invalid", detail: "Stored draft plan could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    logger.LogInformation("Executing draft run {DraftId} for project {ProjectId} operation {OperationId}", draft.DraftId, project.ProjectId, draft.OperationId);
    var run = await runner.RunPlanAsync(plan, project.ProjectKey, tenantContext.TenantId, orgContext.OwnerKey, spec.SpecId, orgContext.OwnerKey, environmentName, ct);
    logger.LogInformation("Stored run {RunId} for project {ProjectId} operation {OperationId}", run.RunId, project.ProjectId, draft.OperationId);
    RecordRunExecuted(httpContext, "drafts.run.execute");

    var runMetadata = JsonSerializer.Serialize(new
    {
        project.ProjectId,
        project.ProjectKey,
        run.OperationId,
        Environment = environmentName
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantContext.TenantId,
        orgContext.UserId,
        AuditActions.RunExecuted,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        runMetadata), ct);

    return Results.Ok(RunMapping.ToDetailDto(run));
}

app.MapPost("/api/ai/explain", ExplainAiEndpointAsync);
app.MapPost("/ai/explain", ExplainAiEndpointAsync);
app.MapPost("/api/v1/ai/explain-operation", ExplainAiEndpointAsync);
app.MapPost("/api/ai/suggest-tests", SuggestAiTestsEndpointAsync);
app.MapPost("/ai/suggest-tests", SuggestAiTestsEndpointAsync);
app.MapPost("/api/v1/ai/suggest-tests", SuggestAiTestsEndpointAsync);
app.MapPost("/api/ai/summarise-run", SummariseRunAiEndpointAsync);
app.MapPost("/ai/summarise-run", SummariseRunAiEndpointAsync);
app.MapPost("/api/v1/ai/summarise-run", SummariseRunAiEndpointAsync);
app.MapPost("/api/ai/suggest-improvements", SuggestImprovementsAiEndpointAsync);
app.MapPost("/ai/suggest-improvements", SuggestImprovementsAiEndpointAsync);
app.MapPost("/api/v1/ai/suggest-improvements", SuggestImprovementsAiEndpointAsync);
app.MapPost("/api/ai/compliance-report", ComplianceReportAiEndpointAsync);
app.MapPost("/ai/compliance-report", ComplianceReportAiEndpointAsync);
app.MapPost("/api/ai/generate-docs", GenerateDocsAiEndpointAsync);
app.MapPost("/ai/generate-docs", GenerateDocsAiEndpointAsync);
app.MapGet("/api/projects/{projectId}/docs/generated", GetGeneratedDocsAsync);
app.MapGet("/projects/{projectId}/docs/generated", GetGeneratedDocsAsync);

app.MapPost("/api/ai/specs/{specId}/summary", async (string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, IAiClient aiClient, SubscriptionEnforcementService subscriptions, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(specId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var tenantContext = httpContext.GetTenantContext();
    var aiGate = await subscriptions.TryConsumeAiAsync(tenantContext.TenantId, ct);
    if (!aiGate.Allowed)
        return SubscriptionProblem(httpContext, aiGate);

    await RecordAiAuditAsync(httpContext, tenantContext.TenantId, "spec.summary", ct);

    var orgContext = httpContext.GetOrgContext();
    var record = await specStore.GetByIdAsync(tenantContext.TenantId, id, ct);
    if (record is null)
        return Results.NotFound();

    var project = await projectStore.GetAsync(tenantContext.TenantId, record.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, record.ProjectId, ct);

    var context = AiContextFactory.BuildSpecSummaryContext(record);
    var specJson = JsonSerializer.Serialize(context, jsonOptions);
    var prompt = AiPromptTemplates.BuildSpecSummaryPrompt(specJson);
    var aiResponse = await aiClient.GetResponseAsync(prompt, ct);
    return Results.Ok(new AiSpecSummaryResponse(record.SpecId, aiResponse.Content));
});

app.Run();

static IResult InvalidRequest(HttpContext context, string detail)
    => ApiProblemFactory.Result(context, StatusCodes.Status400BadRequest, "InvalidRequest", "Invalid request", detail);

static IResult InvalidRequest(string detail)
    => Results.Problem(title: "Invalid request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

static IResult BillingNotConfigured(HttpContext context, IEnumerable<string>? missingKeys = null)
{
    var keyList = missingKeys is null ? string.Empty : string.Join(", ", missingKeys);
    var detail = string.IsNullOrWhiteSpace(keyList)
        ? "Stripe settings are missing. Configure Stripe before using billing endpoints."
        : $"Stripe billing is not configured. Missing required keys: {keyList}.";

    return ApiProblemFactory.Result(context, StatusCodes.Status501NotImplemented, "BillingNotConfigured", "Billing not configured", detail);
}

static void AddZipEntry(ZipArchive archive, string name, string content)
{
    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
    using var entryStream = entry.Open();
    using var writer = new StreamWriter(entryStream, Encoding.UTF8);
    writer.Write(content);
}


static IResult SubscriptionProblem(HttpContext context, SubscriptionGateResult result)
    => ApiProblemFactory.Result(context, result.StatusCode, result.ErrorCode, result.Title, result.Detail);

static IResult SubscriptionProblem(SubscriptionGateResult result)
    => Results.Problem(title: result.Title, detail: result.Detail, statusCode: result.StatusCode);

static void RecordRunExecuted(HttpContext context, string source)
{
    context.RequestServices.GetRequiredService<ApiTesterTelemetry>().RecordRunExecuted(source);

    using var activity = ApiTesterTelemetry.ActivitySource.StartActivity("run.execute", ActivityKind.Internal);
    activity?.SetTag("run.source", source);
}

static void RecordExportGenerated(HttpContext context, string format)
{
    context.RequestServices.GetRequiredService<ApiTesterTelemetry>().RecordExportGenerated(format);

    using var activity = ApiTesterTelemetry.ActivitySource.StartActivity("run.export", ActivityKind.Internal);
    activity?.SetTag("export.format", format);
}

static void ApplyFreePreviewWatermarkIfNeeded(HttpContext context, SubscriptionSnapshot snapshot)
{
    if (snapshot.Subscription.Plan == SubscriptionPlan.Free)
        context.Response.Headers.Append("X-AI-Watermark", "Free preview");
}

static IResult? ValidateRetention(TestRunRecord run, RetentionWindow retention)
{
    if (run.StartedUtc < retention.CutoffUtc)
    {
        return Results.Problem(
            title: "Retention window exceeded",
            detail: $"Runs older than {retention.RetentionDays} days are not available on this plan.",
            statusCode: StatusCodes.Status410Gone);
    }

    return null;
}

static IResult? ValidateRetention(HttpContext context, TestRunRecord run, RetentionWindow retention)
{
    if (run.StartedUtc < retention.CutoffUtc)
    {
        return ApiProblemFactory.Result(context, StatusCodes.Status410Gone, "RetentionWindowExceeded", "Retention window exceeded", $"Runs older than {retention.RetentionDays} days are not available on this plan.");
    }

    return null;
}

static IResult ValidateRetentionOrResult(TestRunRecord run, RetentionWindow retention, object response)
    => ValidateRetention(run, retention) ?? Results.Ok(response);

static IResult ValidateRetentionOrResult(HttpContext context, TestRunRecord run, RetentionWindow retention, object response)
    => ValidateRetention(context, run, retention) ?? Results.Ok(response);

static async Task<int> GetProjectCountAsync(IProjectStore store, Guid tenantId, CancellationToken ct)
{
    var result = await store.ListAsync(
        tenantId,
        new PageRequest(1, 0),
        SortField.CreatedUtc,
        SortDirection.Desc,
        ct);
    return result.Total;
}

static IResult? RequireScope(HttpContext context, string scope)
{
    if (!context.HasScope(scope))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    return null;
}

static IResult? RequireAdminKeyAccess(HttpContext context, OrgContext orgContext)
{
    if (!OrgRoleAccess.CanManageKeys(orgContext.Role))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    return RequireScope(context, ApiKeyScopes.AdminKeys);
}

static async Task<IResult> NotFoundOrForbiddenAsync(IProjectStore store, ITenantContext tenantContext, Guid projectId, CancellationToken ct)
{
    if (await store.ExistsAnyAsync(projectId, ct))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    return Results.NotFound();
}

static ApiPolicyResponse ToPolicyResponse(ApiExecutionPolicy policy)
    => new(
        policy.HostedMode,
        policy.DryRun,
        policy.AllowedBaseUrls.ToList(),
        policy.AllowedMethods.ToList(),
        (int)policy.Timeout.TotalSeconds,
        policy.MaxRequestBodyBytes,
        policy.MaxResponseBodyBytes,
        policy.ValidateSchema,
        policy.BlockLocalhost,
        policy.BlockPrivateNetworks,
        policy.RetryOnFlake,
        policy.MaxRetries);

static bool TryApplyPolicyUpdate(ApiExecutionPolicy policy, ApiPolicyUpdateRequest request, out string error)
{
    error = string.Empty;

    var next = ClonePolicy(policy);

    if (request.HostedMode.HasValue)
        next.HostedMode = request.HostedMode.Value;

    if (request.DryRun.HasValue)
        next.DryRun = request.DryRun.Value;

    if (request.AllowedMethods is not null)
    {
        next.AllowedMethods.Clear();
        foreach (var method in request.AllowedMethods)
        {
            var trimmed = (method ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                next.AllowedMethods.Add(trimmed);
        }
    }

    if (request.AllowedBaseUrls is not null)
    {
        next.AllowedBaseUrls.Clear();
        foreach (var url in request.AllowedBaseUrls)
        {
            var trimmed = (url ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            trimmed = trimmed.TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = $"Invalid allowedBaseUrl: {trimmed}. Must be absolute http/https URL.";
                return false;
            }

            next.AllowedBaseUrls.Add(trimmed);
        }
    }

    if (request.TimeoutSeconds.HasValue)
    {
        var seconds = request.TimeoutSeconds.Value;
        if (seconds < 1) seconds = 1;
        if (seconds > 60) seconds = 60;
        next.Timeout = TimeSpan.FromSeconds(seconds);
    }

    if (request.MaxRequestBodyBytes.HasValue)
    {
        var value = request.MaxRequestBodyBytes.Value;
        if (value < 0)
        {
            error = "maxRequestBodyBytes must be >= 0.";
            return false;
        }

        next.MaxRequestBodyBytes = value;
    }

    if (request.MaxResponseBodyBytes.HasValue)
    {
        var value = request.MaxResponseBodyBytes.Value;
        if (value < 0)
        {
            error = "maxResponseBodyBytes must be >= 0.";
            return false;
        }

        next.MaxResponseBodyBytes = value;
    }

    if (request.ValidateSchema.HasValue)
        next.ValidateSchema = request.ValidateSchema.Value;

    if (request.BlockLocalhost.HasValue)
        next.BlockLocalhost = request.BlockLocalhost.Value;

    if (request.BlockPrivateNetworks.HasValue)
        next.BlockPrivateNetworks = request.BlockPrivateNetworks.Value;

    if (request.RetryOnFlake.HasValue)
        next.RetryOnFlake = request.RetryOnFlake.Value;

    if (request.MaxRetries.HasValue)
    {
        var retries = request.MaxRetries.Value;
        if (retries < 0)
        {
            error = "maxRetries must be >= 0.";
            return false;
        }

        next.MaxRetries = retries;
    }

    if (next.AllowedMethods.Count == 0)
        next.AllowedMethods.Add("GET");

    ApplyPolicy(policy, next);
    return true;
}

static ApiExecutionPolicy ClonePolicy(ApiExecutionPolicy policy)
{
    return new ApiExecutionPolicy
    {
        HostedMode = policy.HostedMode,
        DryRun = policy.DryRun,
        AllowedBaseUrls = policy.AllowedBaseUrls.Select(x => x).ToList(),
        BlockLocalhost = policy.BlockLocalhost,
        BlockPrivateNetworks = policy.BlockPrivateNetworks,
        AllowedMethods = new HashSet<string>(policy.AllowedMethods, StringComparer.OrdinalIgnoreCase),
        Timeout = policy.Timeout,
        MaxRequestBodyBytes = policy.MaxRequestBodyBytes,
        MaxResponseBodyBytes = policy.MaxResponseBodyBytes,
        ValidateSchema = policy.ValidateSchema,
        RetryOnFlake = policy.RetryOnFlake,
        MaxRetries = policy.MaxRetries
    };
}

static void ApplyPolicy(ApiExecutionPolicy target, ApiExecutionPolicy source)
{
    target.HostedMode = source.HostedMode;
    target.DryRun = source.DryRun;
    target.BlockLocalhost = source.BlockLocalhost;
    target.BlockPrivateNetworks = source.BlockPrivateNetworks;
    target.Timeout = source.Timeout;
    target.MaxRequestBodyBytes = source.MaxRequestBodyBytes;
    target.MaxResponseBodyBytes = source.MaxResponseBodyBytes;
    target.ValidateSchema = source.ValidateSchema;
    target.RetryOnFlake = source.RetryOnFlake;
    target.MaxRetries = source.MaxRetries;

    target.AllowedMethods.Clear();
    foreach (var method in source.AllowedMethods)
        target.AllowedMethods.Add(method);

    target.AllowedBaseUrls.Clear();
    foreach (var url in source.AllowedBaseUrls)
        target.AllowedBaseUrls.Add(url);
}

static (string path, OperationType method, OpenApiOperation op)? FindOperation(OpenApiDocument doc, string operationId)
{
    foreach (var path in doc.Paths)
    {
        foreach (var kv in path.Value.Operations)
        {
            var op = kv.Value;
            if (string.Equals(op.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                return (path.Key, kv.Key, op);
        }
    }

    return null;
}

static IEnumerable<(string OperationId, string Path, OperationType Method, OpenApiOperation Operation)> EnumerateOperations(OpenApiDocument doc)
{
    foreach (var path in doc.Paths)
    {
        foreach (var kv in path.Value.Operations)
        {
            var operationId = string.IsNullOrWhiteSpace(kv.Value.OperationId)
                ? $"{kv.Key.ToString().ToUpperInvariant()}:{path.Key}"
                : kv.Value.OperationId;

            yield return (operationId, path.Key, kv.Key, kv.Value);
        }
    }
}

static OpenApiSchemaDto? DescribeSchema(OpenApiSchema? schema)
{
    if (schema is null)
        return null;

    var items = schema.Items is null
        ? null
        : new OpenApiSchemaItemDto(schema.Items.Type ?? string.Empty, schema.Items.Format ?? string.Empty);

    return new OpenApiSchemaDto(
        schema.Type ?? string.Empty,
        schema.Format ?? string.Empty,
        schema.Nullable,
        items);
}

static string ComputeSpecHash(string specJson)
{
    using var sha = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(specJson);
    var hash = sha.ComputeHash(bytes);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static async Task<string> ReadBodyCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
{
    await using var stream = await content.ReadAsStreamAsync(ct);
    using var ms = new MemoryStream();

    var buffer = new byte[8192];
    var total = 0;

    while (true)
    {
        var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
        if (read <= 0) break;

        var remaining = maxBytes - total;
        if (remaining <= 0)
            throw new InvalidOperationException($"OpenAPI spec exceeds {maxBytes} bytes.");

        var toWrite = Math.Min(read, remaining);
        ms.Write(buffer, 0, toWrite);
        total += toWrite;

        if (toWrite < read)
            throw new InvalidOperationException($"OpenAPI spec exceeds {maxBytes} bytes.");
    }

    return Encoding.UTF8.GetString(ms.ToArray());
}

static async Task<IResult> ImportOpenApiSpecAsync(
    string projectId,
    HttpRequest request,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    IHttpClientFactory httpClientFactory,
    SsrfGuard ssrfGuard,
    ApiRuntimeConfig runtime,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct)
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(httpContext, error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var tenantContext = httpContext.GetTenantContext();
    var project = await projectStore.GetAsync(tenantContext.TenantId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, tenantContext, id, ct);

    string? specJson = null;
    string? specUrl = null;

    var specSource = "payload";

    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is not null)
        {
            specSource = "upload";
            if (file.Length > WebOpenApiImportLimits.MaxSpecBytes)
                return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {WebOpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            specJson = await reader.ReadToEndAsync(ct);
        }

        if (specJson is null)
        {
            var path = form["path"].ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                specSource = "path";
                if (!File.Exists(path))
                    return InvalidRequest("Spec path does not exist.");

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > WebOpenApiImportLimits.MaxSpecBytes)
                    return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {WebOpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

                specJson = await File.ReadAllTextAsync(path, ct);
            }
        }

        if (specJson is null)
            specUrl = form["url"].ToString();
    }
    else
    {
        OpenApiImportRequest? payload;
        try
        {
            payload = await request.ReadFromJsonAsync<OpenApiImportRequest>(cancellationToken: ct);
        }
        catch (JsonException)
        {
            return InvalidRequest("OpenAPI import request must be valid JSON.");
        }

        if (!string.IsNullOrWhiteSpace(payload?.Path))
        {
            var path = payload.Path.Trim();
            specSource = "path";
            if (!File.Exists(path))
                return InvalidRequest("Spec path does not exist.");

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > WebOpenApiImportLimits.MaxSpecBytes)
                return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {WebOpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

            specJson = await File.ReadAllTextAsync(path, ct);
        }

        if (specJson is null && !string.IsNullOrWhiteSpace(payload?.Url))
            specUrl = payload.Url.Trim();
    }

    if (specJson is null && !string.IsNullOrWhiteSpace(specUrl))
    {
        specSource = "url";

        if (!Uri.TryCreate(specUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return InvalidRequest("OpenAPI URL must be an absolute http or https URL.");
        }

        var (allowed, reason) = await ssrfGuard.CheckAsync(
            uri,
            runtime.Policy.BlockLocalhost,
            runtime.Policy.BlockPrivateNetworks,
            ct);

        if (!allowed)
            return Results.Problem(title: "OpenAPI URL blocked", detail: reason, statusCode: StatusCodes.Status403Forbidden);

        var client = httpClientFactory.CreateClient(TestPlanRunner.HttpClientName);
        client.Timeout = runtime.Policy.Timeout;

        using var outboundRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await client.SendAsync(outboundRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        HttpResponseMessage finalResponse = response;
        if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is not null)
        {
            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(uri, response.Headers.Location);

            var (redirectAllowed, redirectReason) = await ssrfGuard.CheckAsync(
                redirectUri,
                runtime.Policy.BlockLocalhost,
                runtime.Policy.BlockPrivateNetworks,
                ct);

            if (!redirectAllowed)
                return Results.Problem(title: "OpenAPI URL blocked", detail: $"Redirect blocked by policy: {redirectReason}", statusCode: StatusCodes.Status403Forbidden);

            using var redirectRequest = new HttpRequestMessage(HttpMethod.Get, redirectUri);
            finalResponse = await client.SendAsync(redirectRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        if (!finalResponse.IsSuccessStatusCode)
            return Results.Problem(title: "OpenAPI fetch failed", detail: $"Remote server returned {(int)finalResponse.StatusCode}.", statusCode: StatusCodes.Status400BadRequest);

        if (finalResponse.Content.Headers.ContentLength > WebOpenApiImportLimits.MaxSpecBytes)
            return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {WebOpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

        try
        {
            specJson = await ReadBodyCappedAsync(finalResponse.Content, WebOpenApiImportLimits.MaxSpecBytes, ct);
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {WebOpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }
    }

    if (string.IsNullOrWhiteSpace(specJson))
        return InvalidRequest("Provide an OpenAPI file upload, path, or URL.");

    if (Encoding.UTF8.GetByteCount(specJson) > WebOpenApiImportLimits.MaxSpecBytes)
        return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {WebOpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

    JsonDocument document;
    try
    {
        document = JsonDocument.Parse(specJson);
    }
    catch (JsonException)
    {
        return InvalidRequest("OpenAPI spec must be valid JSON.");
    }

    using (document)
    {
        var title = "Untitled API";
        var version = "unknown";

        if (document.RootElement.TryGetProperty("info", out var info))
        {
            if (info.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
                title = titleElement.GetString() ?? title;

            if (info.TryGetProperty("version", out var versionElement) && versionElement.ValueKind == JsonValueKind.String)
                version = versionElement.GetString() ?? version;
        }

        title = string.IsNullOrWhiteSpace(title) ? "Untitled API" : title.Trim();
        version = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();

        var specHash = ComputeSpecHash(specJson);
        var record = await specStore.UpsertAsync(tenantContext.TenantId, project.ProjectId, title, version, specJson, specHash, DateTime.UtcNow, ct);
        logger.LogInformation(
            "Imported OpenAPI spec for project {ProjectId} titled {Title} version {Version} from {SpecSource}",
            project.ProjectId,
            title,
            version,
            specSource);

        var metadata = JsonSerializer.Serialize(new
        {
            project.ProjectId,
            project.ProjectKey,
            title,
            version,
            Source = specSource
        });

        var route = request.Path.HasValue ? request.Path.Value! : "/projects/{projectId}/openapi/import";

        var auditStore = httpContext.RequestServices.GetRequiredService<IAuditEventStore>();
        await auditStore.CreateAsync(new AuditEventRecord(
            Guid.NewGuid(),
            tenantContext.TenantId,
            orgContext.UserId,
            AuditActions.SpecImported,
            "project",
            project.ProjectId.ToString(),
            DateTime.UtcNow,
            JsonSerializer.Serialize(new
            {
                Route = route,
                Action = AuditActions.SpecImported,
                Metadata = metadata
            })), ct);

        return Results.Ok(OpenApiMapping.ToMetadataDto(record));
    }
}

static bool TryParseReportFormat(string? format, out RunReportFormat reportFormat, out string error)
{
    if (string.IsNullOrWhiteSpace(format))
    {
        reportFormat = RunReportFormat.Markdown;
        error = "Format is required (md or html).";
        return false;
    }

    if (string.Equals(format, "md", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
    {
        reportFormat = RunReportFormat.Markdown;
        error = string.Empty;
        return true;
    }

    if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
    {
        reportFormat = RunReportFormat.Html;
        error = string.Empty;
        return true;
    }

    reportFormat = RunReportFormat.Markdown;
    error = "Format must be 'md' or 'html'.";
    return false;
}

static bool TryParseAuditTimestamp(string? value, string name, out DateTime? parsedUtc, out string error)
{
    parsedUtc = null;
    error = string.Empty;

    if (string.IsNullOrWhiteSpace(value))
        return true;

    if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
    {
        error = $"{name} must be an ISO 8601 timestamp.";
        return false;
    }

    parsedUtc = parsed.ToUniversalTime();
    return true;
}


static async Task RecordAiAuditAsync(HttpContext httpContext, Guid tenantId, string capability, CancellationToken ct)
{
    var orgContext = httpContext.GetOrgContext();
    var auditStore = httpContext.RequestServices.GetRequiredService<IAuditEventStore>();
    var route = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "/api/ai";

    var metadata = JsonSerializer.Serialize(new
    {
        Route = route,
        Action = AuditActions.AiCall,
        Capability = capability
    });

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        tenantId,
        orgContext.UserId,
        AuditActions.AiCall,
        "ai",
        capability,
        DateTime.UtcNow,
        metadata), ct);
}

public partial class Program { }
