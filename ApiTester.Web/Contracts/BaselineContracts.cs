namespace ApiTester.Web.Contracts;

public sealed record BaselineCreateRequest(Guid RunId);

public sealed record BaselineDto(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset SetUtc);

public sealed record BaselineListResponse(IReadOnlyList<BaselineDto> Baselines);
