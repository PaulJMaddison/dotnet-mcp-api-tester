using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.Configure<ApiTester.Site.Services.ApiTesterWebOptions>(
    builder.Configuration.GetSection(ApiTester.Site.Services.ApiTesterWebOptions.SectionName));
builder.Services.AddScoped<ApiTester.Site.Services.IApiKeySession, ApiTester.Site.Services.ApiKeySession>();
builder.Services.AddTransient<ApiTester.Site.Services.ApiKeyHeaderHandler>();
builder.Services.AddHttpClient<ApiTester.Site.Services.IApiTesterWebClient, ApiTester.Site.Services.ApiTesterWebClient>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiTester.Site.Services.ApiTesterWebOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddHttpMessageHandler<ApiTester.Site.Services.ApiKeyHeaderHandler>();
builder.Services.AddHttpClient<ApiTester.Site.Services.LeadCaptureClient>();
builder.Services.AddDbContext<ApiTester.Site.Data.LeadCaptureDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("LeadCapture");
    options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=leads.db" : connectionString);
});
builder.Services.AddScoped<ApiTester.Site.Services.ILeadCaptureStore, ApiTester.Site.Services.LeadCaptureStore>();
builder.Services.AddScoped<ApiTester.Site.Services.ILeadCaptureService, ApiTester.Site.Services.LeadCaptureService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApiTester.Site.Data.LeadCaptureDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapPost("/api/leads", async (
    ApiTester.Site.Models.LeadCaptureRequest request,
    ApiTester.Site.Services.ILeadCaptureService service,
    HttpContext httpContext) =>
{
    var result = await service.SubmitAsync(request, httpContext.RequestAborted);

    if (result.IsHoneypot)
    {
        return Results.Accepted();
    }

    if (!result.IsAccepted)
    {
        return Results.BadRequest(new ApiTester.Site.Models.LeadCaptureErrorResponse(result.Errors));
    }

    return Results.Ok();
});
app.MapRazorComponents<ApiTester.Site.Components.App>();

app.Run();
