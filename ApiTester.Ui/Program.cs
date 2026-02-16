using ApiTester.Ui.Auth;
using ApiTester.Ui.Clients;

var builder = WebApplication.CreateBuilder(args);

var authOptions = builder.Configuration.GetSection(ApiKeyAuthOptions.SectionName).Get<ApiKeyAuthOptions>() ?? new ApiKeyAuthOptions();
var allowedKeys = authOptions.ResolveKeys();
if (allowedKeys.Count == 0)
    throw new InvalidOperationException("UI requires an API key configured (Auth:ApiKey or Auth:ApiKeys).");

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Runs/Details", "/app/runs/{runId:guid}");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddScoped<ApiKeyAuthHandler>();
builder.Services.AddHttpClient<ApiTesterWebClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("ApiTesterWeb:BaseUrl") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
}).AddHttpMessageHandler<ApiKeyAuthHandler>();
builder.Services.AddSingleton(new ApiKeyAuthSettings(allowedKeys));
builder.Services.AddScoped<ApiKeySessionStore>();
builder.Services.AddScoped<ApiTester.Ui.Onboarding.OnboardingSessionStore>();
builder.Services.AddScoped<UiAuthGateMiddleware>();

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseMiddleware<UiAuthGateMiddleware>();
app.MapGet("/ping", () => "ok");
app.MapRazorPages();

app.Run();

public partial class Program { }
