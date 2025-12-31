using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Contracts;
using ApiTester.Web.Mapping;
using ApiTester.Web.Validation;
using Microsoft.Extensions.Options;

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

app.MapGet("/api/projects", async (int? take, IProjectStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryNormalizeTake(take, 50, 1, 200, out var normalizedTake, out var error))
        return InvalidRequest(error);

    var projects = await store.ListAsync(normalizedTake, ct);
    return Results.Ok(ProjectMapping.ToListResponse(normalizedTake, projects));
});

app.MapPost("/api/projects", async (ProjectCreateRequest request, IProjectStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredName(request?.Name, out var error))
        return InvalidRequest(error);

    var project = await store.CreateAsync(request!.Name!, ct);
    return Results.Ok(ProjectMapping.ToCreateResponse(project));
});

app.MapGet("/api/projects/{projectId}", async (string projectId, IProjectStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(projectId, out var id, out var error))
        return InvalidRequest(error);

    var project = await store.GetAsync(id, ct);
    return project is null
        ? Results.NotFound()
        : Results.Ok(ProjectMapping.ToResponse(project));
});

app.MapGet("/api/runs", async (string? projectKey, string? operationId, int? take, ITestRunStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryValidateRequiredKey(projectKey, "projectKey", out var keyError))
        return InvalidRequest(keyError);

    if (!RequestValidation.TryNormalizeTake(take, 20, 1, 200, out var normalizedTake, out var takeError))
        return InvalidRequest(takeError);

    if (!RequestValidation.TryNormalizeOptionalValue(operationId, out var normalizedOperationId, out var opError))
        return InvalidRequest(opError);

    var runs = await store.ListAsync(projectKey!.Trim(), normalizedTake, normalizedOperationId);
    return Results.Ok(RunMapping.ToSummaryResponse(projectKey!.Trim(), normalizedTake, runs));
});

app.MapGet("/api/runs/{runId}", async (string runId, ITestRunStore store, CancellationToken ct) =>
{
    if (!RequestValidation.TryParseGuid(runId, out var id, out var error))
        return InvalidRequest(error);

    var run = await store.GetAsync(id);
    return run is null
        ? Results.NotFound()
        : Results.Ok(RunMapping.ToDetailResponse(run));
});

app.Run();

static IResult InvalidRequest(string detail)
    => Results.Problem(title: "Invalid request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

public partial class Program { }
