using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Web.IntegrationTests;

public sealed class FixtureApiHost : IAsyncDisposable
{
    private readonly IHost _host;

    private FixtureApiHost(IHost host, string baseUrl)
    {
        _host = host;
        BaseUrl = baseUrl.TrimEnd('/');
    }

    public string BaseUrl { get; }

    public static async Task<FixtureApiHost> StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.MapGet("/fixture/status", () => Results.Ok(new { ok = true, service = "fixture-api" }));
        app.MapGet("/fixture/items/{id:int}", (int id) => Results.Ok(new { id, name = $"item-{id}", active = true }));

        await app.StartAsync(ct);

        _ = app.Services.GetRequiredService<IServer>();
        var addresses = app.Urls;
        var baseUrl = addresses.First();

        return new FixtureApiHost(app, baseUrl);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
