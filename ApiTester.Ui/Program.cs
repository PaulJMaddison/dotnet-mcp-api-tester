using ApiTester.Ui.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient<ApiTesterWebClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("ApiTesterWeb:BaseUrl") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

app.MapGet("/ping", () => "ok");
app.MapRazorPages();

app.Run();

public partial class Program { }
