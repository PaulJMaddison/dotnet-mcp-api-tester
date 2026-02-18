namespace ApiTester.McpServer.Persistence.Stores;

public enum SortDirection
{
    Asc,
    Desc
}

public enum SortField
{
    CreatedUtc,
    StartedUtc
}

public sealed record PageRequest(int PageSize, int Offset);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int? NextOffset);

public static class Paging
{
    public static int? CalculateNextOffset(int offset, int itemCount, int total)
    {
        var advancedOffset = (long)offset + itemCount;
        return advancedOffset < total ? (int)advancedOffset : null;
    }
}
