using ApiTester.McpServer.Persistence.Stores;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class PagingTests
{
    [Fact]
    public void CalculateNextOffset_ReturnsNextOffset_WhenMoreItemsExist()
    {
        var result = Paging.CalculateNextOffset(offset: 20, itemCount: 10, total: 100);

        Assert.Equal(30, result);
    }

    [Fact]
    public void CalculateNextOffset_ReturnsNull_WhenPageCompletesTotal()
    {
        var result = Paging.CalculateNextOffset(offset: 90, itemCount: 10, total: 100);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateNextOffset_DoesNotOverflow_WhenOffsetNearIntMax()
    {
        var result = Paging.CalculateNextOffset(int.MaxValue - 2, itemCount: 10, total: int.MaxValue);

        Assert.Null(result);
    }
}
