var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.Configure<ApiTester.Site.Services.ApiTesterWebOptions>(
    builder.Configuration.GetSection(ApiTester.Site.Services.ApiTesterWebOptions.SectionName));
builder.Services.AddHttpClient<ApiTester.Site.Services.IApiTesterWebClient, ApiTester.Site.Services.ApiTesterWebClient>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiTester.Site.Services.ApiTesterWebOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    });

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<ApiTester.Site.Components.App>();

app.Run();
