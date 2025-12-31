using ApiTester.Ui.Auth;
using ApiTester.Ui.Clients;

var builder = WebApplication.CreateBuilder(args);

var authOptions = builder.Configuration.GetSection(ApiKeyAuthOptions.SectionName).Get<ApiKeyAuthOptions>() ?? new ApiKeyAuthOptions();
var allowedKeys = authOptions.ResolveKeys();
if (allowedKeys.Count == 0)
    throw new InvalidOperationException("UI requires an API key configured (Auth:ApiKey or Auth:ApiKeys).");

var primaryApiKey = allowedKeys[0];

builder.Services.AddRazorPages();
builder.Services.AddHttpClient<ApiTesterWebClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("ApiTesterWeb:BaseUrl") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, primaryApiKey);
});
builder.Services.AddSingleton(new ApiKeyAuthSettings(allowedKeys));

var app = builder.Build();

app.UseMiddleware<ApiKeyAuthMiddleware>();
app.MapGet("/ping", () => "ok");
app.MapRazorPages();

app.Run();

public partial class Program { }
