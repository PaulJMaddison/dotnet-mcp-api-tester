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
using ApiTester.Web.Mapping;
using ApiTester.Web.Validation;
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
builder.Services.AddHttpClient();
builder.Services.AddSingleton<RunComparisonService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};

var authOptions = builder.Configuration.GetSection(ApiKeyAuthOptions.SectionName).Get<ApiKeyAuthOptions>() ?? new ApiKeyAuthOptions();
var allowedKeys = authOptions.ResolveKeys();
if (allowedKeys.Count == 0)
    throw new InvalidOperationException("API key authentication requires at least one key in configuration (Auth:ApiKey or Auth:ApiKeys).");
builder.Services.AddSingleton(new ApiKeyAuthSettings(allowedKeys));

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
    context => context.Request.Path.StartsWithSegments("/api"),
    builder => builder.UseMiddleware<ApiKeyAuthMiddleware>());

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

    var ownerKey = httpContext.GetOwnerKey();
    var result = await store.ListAsync(ownerKey, new PageRequest(normalizedPageSize, offset), sortField, direction, ct);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(ProjectMapping.ToListResponse(metadata, result.Items));
});

app.MapPost("/api/projects", async (ProjectCreateRequest request, IProjectStore store, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var error))
        return InvalidRequest(error);

    var ownerKey = httpContext.GetOwnerKey();
    var project = await store.CreateAsync(ownerKey, request!.Name!, ct);
    logger.LogInformation("Created project {ProjectId} for owner {OwnerKey} with name {ProjectName}", project.ProjectId, ownerKey, project.Name);
    return Results.Ok(ProjectMapping.ToDto(project));
});

app.MapGet("/api/projects/{projectId}", async (string projectId, IProjectStore store, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var ownerKey = httpContext.GetOwnerKey();
    var project = await store.GetAsync(ownerKey, id, ct);
    return project is null
        ? await NotFoundOrForbiddenAsync(store, id, ct)
        : Results.Ok(ProjectMapping.ToDto(project));
});

app.MapPost("/api/projects/{projectId}/openapi/import", async (string projectId, HttpRequest request, IProjectStore projectStore, IOpenApiSpecStore specStore, HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetAsync(ownerKey, id, ct);
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
});

app.MapGet("/api/projects/{projectId}/openapi", async (string projectId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetAsync(ownerKey, id, ct);
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

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetAsync(ownerKey, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var records = await specStore.ListAsync(id, ct);
    return Results.Ok(records.Select(OpenApiMapping.ToMetadataDto));
});

app.MapGet("/api/specs/{specId}", async (string specId, IOpenApiSpecStore specStore, IProjectStore projectStore, HttpContext httpContext, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(specId, out var id, out var error))
        return InvalidRequest(error);

    var ownerKey = httpContext.GetOwnerKey();
    var record = await specStore.GetByIdAsync(id, ct);
    if (record is null)
        return Results.NotFound();

    var project = await projectStore.GetAsync(ownerKey, record.ProjectId, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, record.ProjectId, ct);

    return Results.Ok(OpenApiMapping.ToDetailDto(record));
});

app.MapPost("/api/projects/{projectId}/testplans/{operationId}/generate", async (
    string projectId,
    string operationId,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetAsync(ownerKey, id, ct);
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
    var planJson = JsonSerializer.Serialize(plan, jsonOptions);

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

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetAsync(ownerKey, id, ct);
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
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    TestPlanRunner runner,
    ApiRuntimeConfig runtime,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetAsync(ownerKey, id, ct);
    if (project is null)
        return await NotFoundOrForbiddenAsync(projectStore, id, ct);

    var spec = await specStore.GetAsync(id, ct);
    if (spec is null)
        return Results.Problem(title: "OpenAPI spec missing", detail: "Import an OpenAPI spec before executing runs.", statusCode: StatusCodes.Status409Conflict);

    var reader = new OpenApiStringReader();
    var doc = reader.Read(spec.SpecJson, out _);
    if (doc is null)
        return Results.Problem(title: "OpenAPI parse error", detail: "Stored OpenAPI spec could not be parsed.", statusCode: StatusCodes.Status422UnprocessableEntity);

    if (string.IsNullOrWhiteSpace(runtime.BaseUrl))
    {
        var baseUrl = ResolveBaseUrl(doc);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Results.Problem(title: "Base URL missing", detail: "OpenAPI spec does not define servers and no runtime base URL is configured.", statusCode: StatusCodes.Status409Conflict);

        runtime.SetBaseUrl(baseUrl);
    }

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
        var planJson = JsonSerializer.Serialize(plan, jsonOptions);
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
    var run = await runner.RunPlanAsync(plan, project.ProjectKey, ownerKey, spec.SpecId, ct);
    logger.LogInformation("Stored run {RunId} for project {ProjectId} operation {OperationId}", run.RunId, project.ProjectId, trimmedOperationId);
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

    var ownerKey = httpContext.GetOwnerKey();
    var project = await projectStore.GetByKeyAsync(ownerKey, projectKey!.Trim(), ct);
    if (project is null)
        return Results.NotFound();

    logger.LogInformation(
        "Listing runs for project {ProjectKey} operation {OperationId}",
        projectKey!.Trim(),
        normalizedOperationId ?? "(all)");

    var result = await store.ListAsync(
        ownerKey,
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

    var ownerKey = httpContext.GetOwnerKey();
    var run = await store.GetAsync(ownerKey, id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToDetailDto(run));
});

app.MapPost("/api/runs/{runId}/baseline/{baselineRunId}", async (string runId, string baselineRunId, ITestRunStore store, HttpContext httpContext) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    if (!RequestValidation.TryParseGuid(baselineRunId, out var baselineId, out var baselineError))
        return InvalidRequest(baselineError);

    var ownerKey = httpContext.GetOwnerKey();
    var updated = await store.SetBaselineAsync(ownerKey, id, baselineId);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/runs/{runId}/compare/{baselineRunId}", async (string runId, string baselineRunId, ITestRunStore store, RunComparisonService comparison, HttpContext httpContext) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var runError))
        return InvalidRequest(runError);

    if (!RequestValidation.TryParseGuid(baselineRunId, out var baselineId, out var baselineError))
        return InvalidRequest(baselineError);

    var ownerKey = httpContext.GetOwnerKey();
    var run = await store.GetAsync(ownerKey, id);
    if (run is null)
        return Results.NotFound();

    var baseline = await store.GetAsync(ownerKey, baselineId);
    if (baseline is null)
        return Results.NotFound();

    var response = comparison.Compare(run, baseline);
    return Results.Ok(response);
});

app.Run();

static IResult InvalidRequest(string detail)
    => Results.Problem(title: "Invalid request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

static async Task<IResult> NotFoundOrForbiddenAsync(IProjectStore store, Guid projectId, CancellationToken ct)
{
    if (await store.ExistsAsync(projectId, ct))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    return Results.NotFound();
}

static string? ResolveBaseUrl(OpenApiDocument doc)
{
    if (doc.Servers is null || doc.Servers.Count == 0)
        return null;

    var serverUrl = doc.Servers[0].Url;
    return string.IsNullOrWhiteSpace(serverUrl) ? null : serverUrl.Trim().TrimEnd('/');
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

public partial class Program { }
