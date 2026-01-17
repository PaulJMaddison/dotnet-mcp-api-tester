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

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseAntiforgery();
app.MapRazorComponents<ApiTester.Site.Components.App>();

app.Run();
