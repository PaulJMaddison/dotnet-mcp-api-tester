using ApiTester.Web.Validation;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class RequestValidationTests
{
    [Fact]
    public void TryNormalizeTake_UsesDefault_WhenMissing()
    {
        var ok = RequestValidation.TryNormalizeTake(null, 50, 1, 200, out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(50, normalized);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void TryNormalizeTake_ReturnsError_WhenOutOfRange(int value)
    {
        var ok = RequestValidation.TryNormalizeTake(value, 50, 1, 200, out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(50, normalized);
        Assert.Contains("take must be between", error);
    }

    [Fact]
    public void TryValidateRequiredName_ReturnsError_WhenMissing()
    {
        var ok = RequestValidation.TryValidateRequiredName("  ", out var error);

        Assert.False(ok);
        Assert.Equal("Name is required.", error);
    }

    [Fact]
    public void TryParseGuid_ReturnsError_WhenInvalid()
    {
        var ok = RequestValidation.TryParseGuid("not-a-guid", out var parsed, out var error);

        Assert.False(ok);
        Assert.Equal(Guid.Empty, parsed);
        Assert.Equal("Invalid GUID format.", error);
    }
}
