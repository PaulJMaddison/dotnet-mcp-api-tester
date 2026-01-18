using System.Globalization;
using System.Net.Http;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiTester.Web;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using ApiTester.Web.Observability;
using ApiTester.Web.Contracts;
using ApiTester.Web.Comparison;
using ApiTester.Web.Execution;
using ApiTester.Web.Auth;
using ApiTester.Web.AI;
using ApiTester.Web.Diff;
using ApiTester.Web.Entitlements;
using ApiTester.Web.Mapping;
using ApiTester.Web.Reports;
using ApiTester.Web.Validation;
using ApiTester.AI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using WebOpenApiImportLimits = ApiTester.Web.OpenApiImportLimits;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = WebOpenApiImportLimits.MaxRequestBodyBytes;
});

var appConfig = AppConfig.Load(builder.Configuration);
builder.Services.AddSingleton(appConfig);

builder.Services.AddApiTesterPersistence(builder.Configuration);

builder.Services.Configure<ExecutionOptions>(builder.Configuration.GetSection("Execution"));
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ApiRuntimeConfig>(sp =>
{
    var options = sp.GetRequiredService<IOptions<ExecutionOptions>>().Value;
    var runtime = new ApiRuntimeConfig();

    ApiPolicyDefaults.ApplySafeDefaults(runtime.Policy);
    runtime.Policy.DryRun = options.DryRun ?? false;

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
        UseProxy = false
    });
builder.Services.AddHttpClient();
builder.Services.AddSingleton<RunComparisonService>();
builder.Services.AddScoped<BaselineComparisonService>();
builder.Services.AddSingleton<IAiClient, NullAiClient>();
builder.Services.Configure<AiRateLimitOptions>(builder.Configuration.GetSection("AI:RateLimits"));
builder.Services.AddSingleton<AiRateLimiter>();
builder.Services.AddSingleton<IAiProvider, StubAiProvider>();
builder.Services.AddScoped<AiAnalysisService>();
builder.Services.Configure<EntitlementOptions>(builder.Configuration.GetSection("Entitlements"));
builder.Services.AddSingleton<EntitlementService>();
builder.Services.AddScoped<OrgContextResolver>();
builder.Services.AddScoped<IRetentionPruner, RetentionPruner>();

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

var exceptionLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("ApiTester.Web.Exceptions");

app.UseExceptionHandler(config =>
{
    config.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        exceptionLogger.LogError(exception, "Unhandled exception while processing request.");

        var problem = Results.Problem(
            title: "Server error",
            detail: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError);

        await problem.ExecuteAsync(context);
    });
});

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api")
        || context.Request.Path.StartsWithSegments("/api-keys")
        || context.Request.Path.StartsWithSegments("/runs")
        || context.Request.Path.StartsWithSegments("/audit")
        || context.Request.Path.StartsWithSegments("/admin"),
    builder =>
    {
        builder.UseMiddleware<ApiKeyAuthMiddleware>();
        builder.UseMiddleware<OrgContextMiddleware>();
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
            var problem = Results.Problem(
                title: "Request too large",
                detail: $"Request body must be <= {limit} bytes.",
                statusCode: StatusCodes.Status413PayloadTooLarge);

            await problem.ExecuteAsync(context);
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

app.MapGet("/api/orgs/current", async (IOrganisationStore orgStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
    return org is null
        ? Results.NotFound()
        : Results.Ok(OrgMapping.ToDto(org));
});

app.MapGet("/api/orgs/current/members", async (IOrganisationStore orgStore, IUserStore userStore, IMembershipStore membershipStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    if (!OrgRoleAccess.CanViewMembers(orgContext.Role))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
    if (org is null)
        return Results.NotFound();

    var memberships = await membershipStore.ListByOrganisationAsync(orgContext.OrganisationId, ct);
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

app.MapGet("/audit", async (int? take, string? action, string? from, string? to, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var normalizedTake = take ?? 50;
    if (normalizedTake is < 1 or > 200)
        return InvalidRequest("take must be between 1 and 200.");

    if (!TryParseAuditTimestamp(from, "from", out var fromUtc, out var fromError))
        return InvalidRequest(fromError);

    if (!TryParseAuditTimestamp(to, "to", out var toUtc, out var toError))
        return InvalidRequest(toError);

    if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        return InvalidRequest("from must be earlier than to.");

    var records = await auditStore.ListAsync(orgContext.OrganisationId, normalizedTake, action, fromUtc, toUtc, ct);
    return Results.Ok(AuditMapping.ToListResponse(records));
});

app.MapPost("/admin/prune", async (IRetentionPruner pruner, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var result = await pruner.PruneAsync(orgContext.OrganisationId, ct);
    return Results.Ok(new
    {
        result.OrganisationId,
        result.RetentionDays,
        result.CutoffUtc,
        result.RunsPruned
    });
});

app.MapPost("/api-keys", async (ApiKeyCreateRequest request, IApiKeyStore apiKeyStore, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var nameError))
        return InvalidRequest(nameError);

    if (!ApiKeyScopes.TryNormalize(request?.Scopes, out var normalizedScopes, out var scopeError))
        return InvalidRequest(scopeError);

    if (request?.ExpiresUtc is { } expiresUtc && expiresUtc <= DateTime.UtcNow)
        return InvalidRequest("ExpiresUtc must be in the future.");

    var orgContext = httpContext.GetOrgContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var token = ApiKeyToken.Generate();
    var hash = ApiKeyHasher.Hash(token.Token);
    var record = await apiKeyStore.CreateAsync(
        orgContext.OrganisationId,
        orgContext.UserId,
        request!.Name!,
        ApiKeyScopes.Serialize(normalizedScopes),
        request.ExpiresUtc,
        hash,
        token.Prefix,
        ct);

    var metadataJson = JsonSerializer.Serialize(new
    {
        record.Name,
        record.Scopes,
        record.ExpiresUtc
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.ApiKeyCreated,
        "api_key",
        record.KeyId.ToString(),
        DateTime.UtcNow,
        metadataJson), ct);

    return Results.Ok(new ApiKeyCreateResponse(ApiKeyMapping.ToDto(record), token.Token));
});

app.MapGet("/api-keys", async (IApiKeyStore apiKeyStore, HttpContext httpContext, CancellationToken ct) =>
{
    var orgContext = httpContext.GetOrgContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var keys = await apiKeyStore.ListAsync(orgContext.OrganisationId, ct);
    return Results.Ok(ApiKeyMapping.ToListResponse(keys));
});

app.MapPost("/api-keys/{id}/revoke", async (string id, IApiKeyStore apiKeyStore, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(id, out var keyId, out var error))
        return InvalidRequest(error);

    var orgContext = httpContext.GetOrgContext();
    var adminCheck = RequireAdminKeyAccess(httpContext, orgContext);
    if (adminCheck is not null)
        return adminCheck;

    var record = await apiKeyStore.RevokeAsync(orgContext.OrganisationId, keyId, DateTime.UtcNow, ct);
    if (record is null)
        return Results.NotFound();

    var metadataJson = JsonSerializer.Serialize(new
    {
        record.Name,
        record.RevokedUtc
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
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
    var result = await store.ListAsync(orgContext.OrganisationId, new PageRequest(normalizedPageSize, offset), sortField, direction, ct);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(ProjectMapping.ToListResponse(metadata, result.Items));
});

app.MapPost("/api/projects", async (ProjectCreateRequest request, IProjectStore store, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await store.CreateAsync(orgContext.OrganisationId, orgContext.OwnerKey, request!.Name!, ct);
    logger.LogInformation("Created project {ProjectId} for org {OrganisationId} with name {ProjectName}", project.ProjectId, orgContext.OrganisationId, project.Name);
    return Results.Ok(ProjectMapping.ToDto(project));
});

app.MapGet("/api/projects/{projectId}", async (string projectId, IProjectStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await store.GetAsync(orgContext.OrganisationId, id, ct);
    return project is null
        ? await NotFoundOrForbiddenAsync(store, id, ct)
        : Results.Ok(ProjectMapping.ToDto(project));
});

app.MapGet("/api/projects/{projectId}/environments", async (string projectId, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var environments = await environmentStore.ListAsync(orgContext.OwnerKey, id, ct);
    return Results.Ok(EnvironmentMapping.ToListResponse(environments));
});

app.MapPost("/api/projects/{projectId}/environments", async (string projectId, EnvironmentCreateRequest request, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var nameError))
        return InvalidRequest(nameError);

    if (!RequestValidation.TryNormalizeBaseUrl(request?.BaseUrl, out var normalizedBaseUrl, out var baseUrlError))
        return InvalidRequest(baseUrlError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

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
        return InvalidRequest(error);

    if (!RequestValidation.TryParseGuid(environmentId, out var envId, out var envError))
        return InvalidRequest(envError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var environment = await environmentStore.GetAsync(orgContext.OwnerKey, id, envId, ct);
    return environment is null
        ? Results.NotFound()
        : Results.Ok(EnvironmentMapping.ToDto(environment));
});

app.MapPut("/api/projects/{projectId}/environments/{environmentId}", async (string projectId, string environmentId, EnvironmentUpdateRequest request, IProjectStore projectStore, IEnvironmentStore environmentStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (!RequestValidation.TryParseGuid(environmentId, out var envId, out var envError))
        return InvalidRequest(envError);

    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var nameError))
        return InvalidRequest(nameError);

    if (!RequestValidation.TryNormalizeBaseUrl(request?.BaseUrl, out var normalizedBaseUrl, out var baseUrlError))
        return InvalidRequest(baseUrlError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

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
        return InvalidRequest(error);

    if (!RequestValidation.TryParseGuid(environmentId, out var envId, out var envError))
        return InvalidRequest(envError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

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
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var record = await specStore.GetAsync(id, ct);
    return record is null
        ? Results.NotFound()
        : Results.Ok(OpenApiMapping.ToMetadataDto(record));
});

app.MapGet("/api/projects/{projectId}/specs", async (string projectId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var records = await specStore.ListAsync(id, ct);
    return Results.Ok(records.Select(OpenApiMapping.ToMetadataDto));
});

app.MapGet("/api/projects/{projectId}/specs/diff", async (string projectId, string? from, string? to, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        return InvalidRequest("Both from and to spec ids are required.");

    if (!RequestValidation.TryParseGuid(from, out var specAId, out var specAError))
        return InvalidRequest(specAError);

    if (!RequestValidation.TryParseGuid(to, out var specBId, out var specBError))
        return InvalidRequest(specBError);

    var recordA = await specStore.GetByIdAsync(specAId, ct);
    if (recordA is null || recordA.ProjectId != id)
        return Results.NotFound();

    var recordB = await specStore.GetByIdAsync(specBId, ct);
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
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var record = await specStore.GetByIdAsync(id, ct);
    if (record is null)
        return Results.NotFound();

    var project = await projectStore.GetAsync(orgContext.OrganisationId, record.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, record.ProjectId, ct);

    return Results.Ok(OpenApiMapping.ToDetailDto(record));
});

app.MapGet("/specs/{specA}/diff/{specB}", async (string specA, string specB, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(specA, out var specAId, out var specAError))
        return InvalidRequest(specAError);

    if (!RequestValidation.TryParseGuid(specB, out var specBId, out var specBError))
        return InvalidRequest(specBError);

    var orgContext = httpContext.GetOrgContext();

    var recordA = await specStore.GetByIdAsync(specAId, ct);
    if (recordA is null)
        return Results.NotFound();

    var projectA = await projectStore.GetAsync(orgContext.OrganisationId, recordA.ProjectId, ct);
    if (projectA is null)
        return await NotFoundOrForbiddenAsync(projectStore, recordA.ProjectId, ct);

    var recordB = await specStore.GetByIdAsync(specBId, ct);
    if (recordB is null)
        return Results.NotFound();

    var projectB = await projectStore.GetAsync(orgContext.OrganisationId, recordB.ProjectId, ct);
    if (projectB is null)
        return await NotFoundOrForbiddenAsync(projectStore, recordB.ProjectId, ct);

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
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var spec = await specStore.GetAsync(id, ct);
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
    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
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
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

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
    TestPlanRunner runner,
    ApiRuntimeConfig runtime,
    RedactionService redactionService,
    IAuditEventStore auditStore,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var spec = await specStore.GetAsync(id, ct);
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
        var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
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
    var run = await runner.RunPlanAsync(plan, project.ProjectKey, orgContext.OrganisationId, orgContext.OwnerKey, spec.SpecId, orgContext.OwnerKey, environmentName, ct);
    logger.LogInformation("Stored run {RunId} for project {ProjectId} operation {OperationId}", run.RunId, project.ProjectId, trimmedOperationId);

    var runMetadata = JsonSerializer.Serialize(new
    {
        project.ProjectId,
        project.ProjectKey,
        run.OperationId,
        Environment = environmentName
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.RunExecuted,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        runMetadata), ct);

    return Results.Ok(RunMapping.ToDetailDto(run));
});

app.MapGet("/api/runs", async (string? projectKey, string? operationId, int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, ITestRunStore store, IProjectStore projectStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
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
    var project = await projectStore.GetByKeyAsync(orgContext.OrganisationId, projectKey!.Trim(), ct);
    if (project is null)
        return Results.NotFound();

    logger.LogInformation(
        "Listing runs for project {ProjectKey} operation {OperationId}",
        projectKey!.Trim(),
        normalizedOperationId ?? "(all)");

    var result = await store.ListAsync(
        orgContext.OrganisationId,
        projectKey!.Trim(),
        new PageRequest(normalizedPageSize, offset),
        sortField,
        direction,
        normalizedOperationId);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(RunMapping.ToSummaryResponse(projectKey!.Trim(), metadata, result.Items));
});

app.MapGet("/api/runs/{runId}", async (string runId, ITestRunStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToDetailDto(run));
});

app.MapGet("/api/runs/{runId}/summary", async (string runId, ITestRunStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToSummaryCounts(run));
});

app.MapGet("/api/runs/{runId}/audit", async (string runId, ITestRunStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToAuditResponse(run));
});

app.MapGet("/api/runs/{runId}/compliance-report", async (string runId, ITestRunStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToComplianceReport(run));
});

app.MapGet("/api/runs/{runId}/annotations", async (string runId, ITestRunStore runStore, IRunAnnotationStore annotationStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await runStore.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var annotations = await annotationStore.ListAsync(orgContext.OwnerKey, id, ct);
    return Results.Ok(RunAnnotationMapping.ToListResponse(id, annotations));
});

app.MapPost("/api/runs/{runId}/annotations", async (string runId, RunAnnotationCreateRequest request, ITestRunStore runStore, IRunAnnotationStore annotationStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    if (!RequestValidation.TryNormalizeAnnotationNote(request?.Note, out var normalizedNote, out var noteError))
        return InvalidRequest(noteError);

    if (!RequestValidation.TryNormalizeOptionalJiraLink(request?.JiraLink, out var normalizedJiraLink, out var jiraError))
        return InvalidRequest(jiraError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await runStore.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var annotation = await annotationStore.CreateAsync(orgContext.OwnerKey, id, normalizedNote, normalizedJiraLink, ct);
    logger.LogInformation("Created annotation {AnnotationId} for run {RunId}", annotation.AnnotationId, id);
    return Results.Ok(RunAnnotationMapping.ToDto(annotation));
});

app.MapGet("/api/runs/{runId}/annotations/{annotationId}", async (string runId, string annotationId, ITestRunStore runStore, IRunAnnotationStore annotationStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    if (!RequestValidation.TryParseGuid(annotationId, out var annotationGuid, out var annotationError))
        return InvalidRequest(annotationError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await runStore.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var annotation = await annotationStore.GetAsync(orgContext.OwnerKey, id, annotationGuid, ct);
    return annotation is null
        ? Results.NotFound()
        : Results.Ok(RunAnnotationMapping.ToDto(annotation));
});

app.MapPut("/api/runs/{runId}/annotations/{annotationId}", async (string runId, string annotationId, RunAnnotationUpdateRequest request, ITestRunStore runStore, IRunAnnotationStore annotationStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

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
    var run = await runStore.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var annotation = await annotationStore.UpdateAsync(orgContext.OwnerKey, id, annotationGuid, normalizedNote, normalizedJiraLink, ct);
    if (annotation is null)
        return Results.NotFound();

    logger.LogInformation("Updated annotation {AnnotationId} for run {RunId}", annotationGuid, id);
    return Results.Ok(RunAnnotationMapping.ToDto(annotation));
});

app.MapDelete("/api/runs/{runId}/annotations/{annotationId}", async (string runId, string annotationId, ITestRunStore runStore, IRunAnnotationStore annotationStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    if (!RequestValidation.TryParseGuid(annotationId, out var annotationGuid, out var annotationError))
        return InvalidRequest(annotationError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await runStore.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var removed = await annotationStore.DeleteAsync(orgContext.OwnerKey, id, annotationGuid, ct);
    if (!removed)
        return Results.NotFound();

    logger.LogInformation("Deleted annotation {AnnotationId} for run {RunId}", annotationGuid, id);
    return Results.NoContent();
});

app.MapGet("/api/runs/{runId}/report", async (string runId, string? format, ITestRunStore store, EntitlementService entitlements, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanExport)
        return FeatureNotAvailable("Export", entitlements);

    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    if (!TryParseReportFormat(format, out var reportFormat, out var formatError))
        return InvalidRequest(formatError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var report = RunReportGenerator.Generate(run, reportFormat);
    var contentType = reportFormat == RunReportFormat.Markdown
        ? "text/markdown; charset=utf-8"
        : "text/html; charset=utf-8";

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = reportFormat.ToString()
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    return Results.Text(report, contentType);
});

app.MapGet("/runs/{runId}/export/junit", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, EntitlementService entitlements, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanExport)
        return FeatureNotAvailable("Export", entitlements);

    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var content = RunExportGenerator.GenerateJunit(redactedRun);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "junit"
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    return Results.Text(content, "application/junit+xml; charset=utf-8");
});

app.MapGet("/runs/{runId}/export/json", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, EntitlementService entitlements, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanExport)
        return FeatureNotAvailable("Export", entitlements);

    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var payload = RunExportGenerator.GenerateJson(redactedRun, jsonOptions);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "json"
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    return Results.Text(payload, "application/json; charset=utf-8");
});

app.MapGet("/runs/{runId}/export/csv", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, EntitlementService entitlements, IAuditEventStore auditStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanExport)
        return FeatureNotAvailable("Export", entitlements);

    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var payload = RunExportGenerator.GenerateCsv(redactedRun);

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "csv"
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    return Results.Text(payload, "text/csv; charset=utf-8");
});

app.MapGet("/runs/{runId}/export/evidence-bundle", async (string runId, ITestRunStore store, IOrganisationStore orgStore, RedactionService redactionService, IAuditEventStore auditStore, EntitlementService entitlements, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanExport)
        return FeatureNotAvailable("Export", entitlements);

    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var org = await orgStore.GetAsync(orgContext.OrganisationId, ct);
    var redactedRun = RunExportRedactor.RedactRun(run, redactionService, org?.RedactionRules);
    var runJson = RunExportGenerator.GenerateJson(redactedRun, jsonOptions);
    var policyJson = JsonSerializer.Serialize(new
    {
        redactedRun.RunId,
        redactedRun.PolicySnapshot
    }, jsonOptions);

    var auditEvents = await auditStore.ListAsync(orgContext.OrganisationId, 200, null, null, null, ct);
    var auditSubset = auditEvents
        .Where(evt => string.Equals(evt.TargetType, "run", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(evt.TargetId, redactedRun.RunId.ToString(), StringComparison.OrdinalIgnoreCase))
        .Take(100)
        .ToList();

    var auditJson = JsonSerializer.Serialize(new
    {
        redactedRun.RunId,
        Events = auditSubset
    }, jsonOptions);

    using var stream = new MemoryStream();
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
    {
        AddZipEntry(archive, "run.json", runJson);
        AddZipEntry(archive, "policy.json", policyJson);
        AddZipEntry(archive, "audit.json", auditJson);
    }

    var exportMetadata = JsonSerializer.Serialize(new
    {
        Format = "evidence-bundle"
    }, jsonOptions);

    await auditStore.CreateAsync(new AuditEventRecord(
        Guid.NewGuid(),
        orgContext.OrganisationId,
        orgContext.UserId,
        AuditActions.ExportGenerated,
        "run",
        run.RunId.ToString(),
        DateTime.UtcNow,
        exportMetadata), ct);

    return Results.File(stream.ToArray(), "application/zip", $"{redactedRun.RunId}-evidence.zip");
});

app.MapPost("/api/runs/{runId}/baseline/{baselineRunId}", async (string runId, string baselineRunId, ITestRunStore store, HttpContext httpContext) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    if (!RequestValidation.TryParseGuid(baselineRunId, out var baselineId, out var baselineError))
        return InvalidRequest(baselineError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var updated = await store.SetBaselineAsync(orgContext.OrganisationId, id, baselineId);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/baselines", async (BaselineCreateRequest request, ITestRunStore store, IBaselineStore baselineStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (request is null || request.RunId == Guid.Empty)
        return InvalidRequest("runId is required.");

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, request.RunId);
    if (run is null)
        return Results.NotFound();

    var baseline = await baselineStore.SetAsync(orgContext.OrganisationId, run.ProjectKey, run.OperationId, run.RunId, ct);
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
    var baselines = await baselineStore.ListAsync(orgContext.OrganisationId, projectKey, operationId, take ?? 50, ct);
    var response = baselines
        .Select(baseline => new BaselineDto(baseline.RunId, baseline.ProjectKey, baseline.OperationId, baseline.SetUtc))
        .ToList();

    return Results.Ok(new BaselineListResponse(response));
});

app.MapGet("/api/runs/{runId}/compare/{baselineRunId}", async (string runId, string baselineRunId, ITestRunStore store, RunComparisonService comparison, HttpContext httpContext) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    if (!RequestValidation.TryParseGuid(baselineRunId, out var baselineId, out var baselineError))
        return InvalidRequest(baselineError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var baseline = await store.GetAsync(orgContext.OrganisationId, baselineId);
    if (baseline is null)
        return Results.NotFound();

    var response = comparison.Compare(run, baseline);
    return Results.Ok(response);
});

app.MapGet("/api/runs/{runId}/compare-to-baseline", async (string runId, BaselineComparisonService comparison, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var result = await comparison.CompareAsync(orgContext.OrganisationId, id, ct);

    return result.Status switch
    {
        BaselineComparisonStatus.Ok => Results.Ok(result.Response),
        BaselineComparisonStatus.RunNotFound => Results.NotFound(),
        _ => Results.NotFound()
    };
});

app.MapPost("/api/ai/runs/{runId}/explanation", async (string runId, ITestRunStore store, IAiClient aiClient, EntitlementService entitlements, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanUseAi)
        return FeatureNotAvailable("AI", entitlements);

    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.RunsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var run = await store.GetAsync(orgContext.OrganisationId, id);
    if (run is null)
        return Results.NotFound();

    var context = AiContextFactory.BuildRunExplanationContext(run);
    var runJson = JsonSerializer.Serialize(context, jsonOptions);
    var prompt = AiPromptTemplates.BuildRunExplanationPrompt(runJson);
    var aiResponse = await aiClient.GetResponseAsync(prompt, ct);
    return Results.Ok(new AiRunExplanationResponse(run.RunId, aiResponse.Content));
});

app.MapPost("/api/ai/specs/{specId}/summary", async (string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, IAiClient aiClient, EntitlementService entitlements, HttpContext httpContext, CancellationToken ct) =>
{
    if (!entitlements.CanUseAi)
        return FeatureNotAvailable("AI", entitlements);

    if (!RequestValidation.TryParseGuid(specId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsRead);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var record = await specStore.GetByIdAsync(id, ct);
    if (record is null)
        return Results.NotFound();

    var project = await projectStore.GetAsync(orgContext.OrganisationId, record.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, record.ProjectId, ct);

    var context = AiContextFactory.BuildSpecSummaryContext(record);
    var specJson = JsonSerializer.Serialize(context, jsonOptions);
    var prompt = AiPromptTemplates.BuildSpecSummaryPrompt(specJson);
    var aiResponse = await aiClient.GetResponseAsync(prompt, ct);
    return Results.Ok(new AiSpecSummaryResponse(record.SpecId, aiResponse.Content));
});

app.Run();

static IResult InvalidRequest(string detail)
    => Results.Problem(title: "Invalid request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

static void AddZipEntry(ZipArchive archive, string name, string content)
{
    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
    using var entryStream = entry.Open();
    using var writer = new StreamWriter(entryStream, Encoding.UTF8);
    writer.Write(content);
}

static IResult FeatureNotAvailable(string feature, EntitlementService entitlements)
    => Results.Problem(
        title: "Feature not available",
        detail: $"{feature} features require a Pro subscription. Current tier: {entitlements.Tier}.",
        statusCode: StatusCodes.Status403Forbidden);

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

static async Task<IResult> NotFoundOrForbiddenAsync(IProjectStore store, Guid projectId, CancellationToken ct)
{
    if (await store.ExistsAsync(projectId, ct))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    return Results.NotFound();
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

static string ComputeSpecHash(string specJson)
{
    using var sha = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(specJson);
    var hash = sha.ComputeHash(bytes);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static async Task<IResult> ImportOpenApiSpecAsync(
    string projectId,
    HttpRequest request,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct)
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var scopeCheck = RequireScope(httpContext, ApiKeyScopes.ProjectsWrite);
    if (scopeCheck is not null)
        return scopeCheck;

    var orgContext = httpContext.GetOrgContext();
    var project = await projectStore.GetAsync(orgContext.OrganisationId, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    string? specJson = null;

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
    }

    if (string.IsNullOrWhiteSpace(specJson))
        return InvalidRequest("Provide an OpenAPI file upload or path.");

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
        var record = await specStore.UpsertAsync(project.ProjectId, title, version, specJson, specHash, DateTime.UtcNow, ct);
        logger.LogInformation(
            "Imported OpenAPI spec for project {ProjectId} titled {Title} version {Version} from {SpecSource}",
            project.ProjectId,
            title,
            version,
            specSource);
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

public partial class Program { }
