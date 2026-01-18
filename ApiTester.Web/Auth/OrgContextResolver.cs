using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ApiTester.Web.Auth;

public sealed class OrgContextResolver
{
    private readonly IUserStore _users;
    private readonly IMembershipStore _memberships;
    private readonly IHostEnvironment _env;

    public OrgContextResolver(
        IUserStore users,
        IMembershipStore memberships,
        IHostEnvironment env)
    {
        _users = users;
        _memberships = memberships;
        _env = env;
    }

    public async Task<OrgContext?> ResolveAsync(HttpContext context, CancellationToken ct)
    {
        var apiKeyContext = context.GetApiKeyContext();
        if (apiKeyContext is null)
            return null;

        var user = await _users.GetAsync(apiKeyContext.UserId, ct);
        if (user is null)
        {
            return null;
        }

        var membership = await _memberships.GetAsync(apiKeyContext.OrganisationId, user.UserId, ct);
        if (membership is null)
        {
            return null;
        }

        return new OrgContext(membership.OrganisationId, user.UserId, membership.Role, user.ExternalId, IsLocalDev());
    }

    private bool IsLocalDev() => _env.IsDevelopment();
}
