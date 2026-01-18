using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IMembershipStore
{
    Task<MembershipRecord> CreateAsync(Guid organisationId, Guid userId, OrgRole role, CancellationToken ct);
    Task<MembershipRecord?> GetAsync(Guid organisationId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<MembershipRecord>> ListByOrganisationAsync(Guid organisationId, CancellationToken ct);
    Task<IReadOnlyList<MembershipRecord>> ListByUserAsync(Guid userId, CancellationToken ct);
}
