using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiTester.Web.Auth;

public sealed class OrgContextResolver
{
    private readonly IOrganisationStore _organisations;
    private readonly IUserStore _users;
    private readonly IMembershipStore _memberships;
    private readonly IHostEnvironment _env;
    private readonly ILogger<OrgContextResolver> _logger;

    public OrgContextResolver(
        IOrganisationStore organisations,
        IUserStore users,
        IMembershipStore memberships,
        IHostEnvironment env,
        ILogger<OrgContextResolver> logger)
    {
        _organisations = organisations;
        _users = users;
        _memberships = memberships;
        _env = env;
        _logger = logger;
    }

    public async Task<OrgContext?> ResolveAsync(HttpContext context, CancellationToken ct)
    {
        var ownerKey = context.GetOwnerKey();
        if (string.IsNullOrWhiteSpace(ownerKey))
            return null;

        var user = await _users.GetByExternalIdAsync(ownerKey, ct);
        if (user is null)
        {
            if (!IsLocalDev())
                return null;

            var displayName = $"Local {ownerKey}";
            user = await _users.CreateAsync(ownerKey, displayName, null, ct);
        }

        var memberships = await _memberships.ListByUserAsync(user.UserId, ct);
        var membership = memberships.FirstOrDefault();
        if (membership is null)
        {
            if (!IsLocalDev())
                return null;

            var org = await EnsureDefaultOrgAsync(ct);
            membership = await _memberships.CreateAsync(org.OrganisationId, user.UserId, OrgRole.Owner, ct);
            _logger.LogInformation("Bootstrapped local dev org {OrganisationId} for user {UserId}", org.OrganisationId, user.UserId);
        }

        return new OrgContext(membership.OrganisationId, user.UserId, membership.Role, ownerKey, IsLocalDev());
    }

    private bool IsLocalDev() => _env.IsDevelopment();

    private async Task<OrganisationRecord> EnsureDefaultOrgAsync(CancellationToken ct)
    {
        var existing = await _organisations.GetBySlugAsync(OrgDefaults.DefaultOrganisationSlug, ct);
        if (existing is not null)
            return existing;

        return await _organisations.CreateAsync(OrgDefaults.DefaultOrganisationName, OrgDefaults.DefaultOrganisationSlug, ct);
    }
}
