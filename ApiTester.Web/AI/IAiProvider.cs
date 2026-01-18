namespace ApiTester.Web.AI;

public interface IAiProvider
{
    Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct);
}
