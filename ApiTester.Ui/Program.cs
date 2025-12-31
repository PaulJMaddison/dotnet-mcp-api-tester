var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

app.MapGet("/ping", () => "ok");
app.MapRazorPages();

app.Run();
