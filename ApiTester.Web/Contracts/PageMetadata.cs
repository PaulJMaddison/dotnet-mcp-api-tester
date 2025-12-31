namespace ApiTester.Web.Contracts;

public sealed record PageMetadata(int Total, int PageSize, string? NextPageToken);
