using ApiTester.Site.Data;
using ApiTester.Site.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
});
builder.Services.Configure<ApiTesterWebOptions>(
    builder.Configuration.GetSection(ApiTesterWebOptions.SectionName));
builder.Services.AddTransient<ApiKeyHeaderHandler>();
builder.Services.AddHttpClient<IApiTesterWebClient, ApiTesterWebClient>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiTesterWebOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddHttpMessageHandler<ApiKeyHeaderHandler>();
builder.Services.AddHttpClient<LeadCaptureClient>();

builder.Services.AddDbContext<LeadCaptureDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("LeadCapture");
    options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=leads.db" : connectionString);
});

builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Identity");
    options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=identity.db" : connectionString);
});

builder.Services.AddScoped<ILeadCaptureStore, LeadCaptureStore>();
builder.Services.AddScoped<ILeadCaptureService, LeadCaptureService>();
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.AddScoped<ITenantBootstrapper, TenantBootstrapper>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/signin";
        options.LogoutPath = "/signout";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"] ?? string.Empty;
        options.ClientId = builder.Configuration["Auth:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Auth:ClientSecret"] ?? string.Empty;
        options.CallbackPath = builder.Configuration["Auth:CallbackPath"] ?? "/signin-oidc";
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        var configuredScopes = builder.Configuration.GetSection("Auth:Scopes").Get<string[]>() ?? Array.Empty<string>();
        if (configuredScopes.Length == 0)
        {
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        }
        else
        {
            foreach (var scope in configuredScopes.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                options.Scope.Add(scope);
            }
        }
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var path = context.Context.Request.Path;

        if (path.StartsWithSegments("/assets")
            || path.StartsWithSegments("/images")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js"))
        {
            context.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=31536000,immutable";
            return;
        }

        if (path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=3600";
        }
    }
});

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var leadDbContext = scope.ServiceProvider.GetRequiredService<LeadCaptureDbContext>();
    leadDbContext.Database.EnsureCreated();

    var identityDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    identityDbContext.Database.EnsureCreated();
}

app.MapGet("/signin", async (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/app");
    }

    await httpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = "/app"
    });

    return Results.Empty;
});

app.MapGet("/signout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await httpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = "/"
    });

    return Results.Empty;
});

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

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/app") && context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.Redirect("/signin");
        return;
    }

    if (context.User.Identity?.IsAuthenticated == true)
    {
        using var scope = context.RequestServices.CreateScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<ITenantBootstrapper>();
        await bootstrapper.EnsureUserTenantMembershipAsync(context.User, context.RequestAborted);
    }

    await next();
});

app.MapRazorComponents<ApiTester.Site.Components.App>();

app.Run();

public partial class Program;
