using System.Text;
using System.Text.Json;
using ApiTester.Web;
using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using ApiTester.Web.Contracts;
using ApiTester.Web.Mapping;
using ApiTester.Web.Validation;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

var builder = WebApplication.CreateBuilder(args);

var appConfig = AppConfig.Load(builder.Configuration);
builder.Services.AddSingleton(appConfig);

builder.Services.AddApiTesterPersistence(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

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

app.MapGet("/api/projects", async (int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, IProjectStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryNormalizePageSize(pageSize, take, 50, 1, 200, out var normalizedPageSize, out var sizeError))
        return InvalidRequest(sizeError);

    if (!RequestValidation.TryNormalizePageToken(pageToken, skip, out var offset, out var tokenError))
        return InvalidRequest(tokenError);

    if (!RequestValidation.TryNormalizeSort(sort, SortField.CreatedUtc, out var sortField, out var sortError))
        return InvalidRequest(sortError);

    if (!RequestValidation.TryNormalizeOrder(order, SortDirection.Desc, out var direction, out var orderError))
        return InvalidRequest(orderError);

    var result = await store.ListAsync(new PageRequest(normalizedPageSize, offset), sortField, direction, ct);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(ProjectMapping.ToListResponse(metadata, result.Items));
});

app.MapPost("/api/projects", async (ProjectCreateRequest request, IProjectStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var error))
        return InvalidRequest(error);

    var project = await store.CreateAsync(request!.Name!, ct);
    return Results.Ok(ProjectMapping.ToDto(project));
});

app.MapGet("/api/projects/{projectId}", async (string projectId, IProjectStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var project = await store.GetAsync(id, ct);
    return project is null
        ? Results.NotFound()
        : Results.Ok(ProjectMapping.ToDto(project));
});

app.MapPost("/api/projects/{projectId}/openapi/import", async (string projectId, HttpRequest request, IProjectStore projectStore, IOpenApiSpecStore specStore, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var project = await projectStore.GetAsync(id, ct);
    if (project is null)
        return Results.NotFound();

    string? specJson = null;

    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is not null)
        {
            if (file.Length > OpenApiImportLimits.MaxSpecBytes)
                return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {OpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            specJson = await reader.ReadToEndAsync(ct);
        }

        if (specJson is null)
        {
            var path = form["path"].ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!File.Exists(path))
                    return InvalidRequest("Spec path does not exist.");

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > OpenApiImportLimits.MaxSpecBytes)
                    return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {OpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

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
            if (!File.Exists(path))
                return InvalidRequest("Spec path does not exist.");

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > OpenApiImportLimits.MaxSpecBytes)
                return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {OpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

            specJson = await File.ReadAllTextAsync(path, ct);
        }
    }

    if (string.IsNullOrWhiteSpace(specJson))
        return InvalidRequest("Provide an OpenAPI file upload or path.");

    if (Encoding.UTF8.GetByteCount(specJson) > OpenApiImportLimits.MaxSpecBytes)
        return Results.Problem(title: "OpenAPI spec too large", detail: $"Spec must be <= {OpenApiImportLimits.MaxSpecBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);

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

        var record = await specStore.UpsertAsync(project.ProjectId, title, version, specJson, DateTime.UtcNow, ct);
        return Results.Ok(OpenApiMapping.ToMetadataDto(record));
    }
});

app.MapGet("/api/projects/{projectId}/openapi", async (string projectId, IOpenApiSpecStore specStore, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var record = await specStore.GetAsync(id, ct);
    return record is null
        ? Results.NotFound()
        : Results.Ok(OpenApiMapping.ToMetadataDto(record));
});

app.MapPost("/api/projects/{projectId}/testplans/{operationId}/generate", async (
    string projectId,
    string operationId,
    IProjectStore projectStore,
    IOpenApiSpecStore specStore,
    ITestPlanStore planStore,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var project = await projectStore.GetAsync(id, ct);
    if (project is null)
        return Results.NotFound();

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
    var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });

    var record = await planStore.UpsertAsync(id, operationId.Trim(), planJson, DateTime.UtcNow, ct);
    return Results.Ok(new TestPlanResponse(record.ProjectId, record.OperationId, record.PlanJson, record.CreatedUtc));
});

app.MapGet("/api/projects/{projectId}/testplans/{operationId}", async (
    string projectId,
    string operationId,
    IProjectStore projectStore,
    ITestPlanStore planStore,
    CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    if (string.IsNullOrWhiteSpace(operationId))
        return InvalidRequest("operationId is required.");

    var project = await projectStore.GetAsync(id, ct);
    if (project is null)
        return Results.NotFound();

    var record = await planStore.GetAsync(id, operationId.Trim(), ct);
    return record is null
        ? Results.NotFound()
        : Results.Ok(new TestPlanResponse(record.ProjectId, record.OperationId, record.PlanJson, record.CreatedUtc));
});

app.MapGet("/api/runs", async (string? projectKey, string? operationId, int? pageSize, string? pageToken, int? skip, string? sort, string? order, int? take, ITestRunStore store, CancellationToken ct) =>
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

    var result = await store.ListAsync(
        projectKey!.Trim(),
        new PageRequest(normalizedPageSize, offset),
        sortField,
        direction,
        normalizedOperationId);
    var metadata = new PageMetadata(result.Total, normalizedPageSize, result.NextOffset?.ToString());
    return Results.Ok(RunMapping.ToSummaryResponse(projectKey!.Trim(), metadata, result.Items));
});

app.MapGet("/api/runs/{runId}", async (string runId, ITestRunStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var run = await store.GetAsync(id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToDetailDto(run));
});

app.Run();

static IResult InvalidRequest(string detail)
    => Results.Problem(title: "Invalid request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

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

public partial class Program { }
