using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IOrganisationStore
{
    Task<OrganisationRecord> CreateAsync(string name, string slug, CancellationToken ct);
    Task<OrganisationRecord?> GetAsync(Guid organisationId, CancellationToken ct);
    Task<OrganisationRecord?> GetBySlugAsync(string slug, CancellationToken ct);
}
