using System.Security.Claims;
using ApiTester.Site.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Site.Services;

public interface ITenantBootstrapper
{
    Task EnsureUserTenantMembershipAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<AccountSummary?> GetAccountSummaryAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public sealed class TenantBootstrapper : ITenantBootstrapper
{
    private readonly IdentityDbContext _dbContext;

    public TenantBootstrapper(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureUserTenantMembershipAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        var displayName = principal.FindFirstValue("name") ?? email ?? externalId;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(externalId))
        {
            return;
        }

        var user = await _dbContext.Users
            .Include(x => x.Memberships)
            .FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                ExternalId = externalId,
                Email = email,
                DisplayName = displayName ?? email
            };
            _dbContext.Users.Add(user);
        }
        else
        {
            user.Email = email;
            user.DisplayName = displayName ?? email;
        }

        var hasMembership = user.Memberships.Count != 0 || await _dbContext.TenantMembers.AnyAsync(x => x.UserId == user.UserId, cancellationToken);
        if (!hasMembership)
        {
            var tenant = new Tenant
            {
                Name = CreateTenantName(email)
            };

            _dbContext.Tenants.Add(tenant);
            _dbContext.TenantMembers.Add(new TenantMember
            {
                Tenant = tenant,
                User = user,
                Role = TenantRole.Owner
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountSummary?> GetAccountSummaryAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        var user = await _dbContext.Users
            .Where(x => x.ExternalId == externalId)
            .Select(x => new
            {
                x.Email,
                x.DisplayName,
                Membership = x.Memberships.Select(m => new { m.Role, TenantName = m.Tenant.Name }).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || user.Membership is null)
        {
            return null;
        }

        return new AccountSummary(user.Email, user.DisplayName, user.Membership.TenantName, user.Membership.Role);
    }

    private static string CreateTenantName(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return "Personal workspace";
        }

        var domain = email[(atIndex + 1)..].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain))
        {
            return "Personal workspace";
        }

        var firstLabel = domain.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLabel))
        {
            return "Personal workspace";
        }

        return $"{char.ToUpperInvariant(firstLabel[0])}{firstLabel[1..]} workspace";
    }
}

public sealed record AccountSummary(string Email, string DisplayName, string TenantName, TenantRole Role);
