using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Validation;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class RequestValidationTests
{
    [Fact]
    public void TryNormalizePageSize_UsesDefault_WhenMissing()
    {
        var ok = RequestValidation.TryNormalizePageSize(null, null, 50, 1, 200, out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(50, normalized);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void TryNormalizePageSize_ReturnsError_WhenOutOfRange(int value)
    {
        var ok = RequestValidation.TryNormalizePageSize(value, null, 50, 1, 200, out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(50, normalized);
        Assert.Contains("pageSize must be between", error);
    }

    [Fact]
    public void TryNormalizePageSize_UsesTakeFallback()
    {
        var ok = RequestValidation.TryNormalizePageSize(null, 25, 50, 1, 200, out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(25, normalized);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryNormalizeSort_UsesDefault_WhenMissing()
    {
        var ok = RequestValidation.TryNormalizeSort(null, SortField.CreatedUtc, out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(SortField.CreatedUtc, normalized);
        Assert.Equal(string.Empty, error);
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

    [Fact]
    public void TryNormalizeBaseUrl_ReturnsError_WhenMissing()
    {
        var ok = RequestValidation.TryNormalizeBaseUrl("  ", out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.Equal("baseUrl is required.", error);
    }

    [Fact]
    public void TryNormalizeBaseUrl_NormalizesHttpUrls()
    {
        var ok = RequestValidation.TryNormalizeBaseUrl("https://example.com/api/", out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("https://example.com/api", normalized);
    }

    [Fact]
    public void TryNormalizeAnnotationNote_ReturnsError_WhenMissing()
    {
        var ok = RequestValidation.TryNormalizeAnnotationNote("   ", out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.Equal("note is required.", error);
    }

    [Fact]
    public void TryNormalizeAnnotationNote_ReturnsError_WhenTooLong()
    {
        var note = new string('a', RequestValidation.MaxAnnotationNoteLength + 1);

        var ok = RequestValidation.TryNormalizeAnnotationNote(note, out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.Contains("note must be", error);
    }

    [Fact]
    public void TryNormalizeAnnotationNote_NormalizesText()
    {
        var ok = RequestValidation.TryNormalizeAnnotationNote("  Looks good  ", out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("Looks good", normalized);
    }

    [Fact]
    public void TryNormalizeOptionalJiraLink_ReturnsError_WhenWhitespace()
    {
        var ok = RequestValidation.TryNormalizeOptionalJiraLink("   ", out var normalized, out var error);

        Assert.False(ok);
        Assert.Null(normalized);
        Assert.Equal("jiraLink cannot be empty.", error);
    }

    [Fact]
    public void TryNormalizeOptionalJiraLink_ReturnsError_WhenInvalid()
    {
        var ok = RequestValidation.TryNormalizeOptionalJiraLink("ftp://jira.example.com/TIX-1", out var normalized, out var error);

        Assert.False(ok);
        Assert.Null(normalized);
        Assert.Equal("jiraLink must be an absolute http or https URL.", error);
    }

    [Fact]
    public void TryNormalizeOptionalJiraLink_AllowsNull()
    {
        var ok = RequestValidation.TryNormalizeOptionalJiraLink(null, out var normalized, out var error);

        Assert.True(ok);
        Assert.Null(normalized);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryNormalizeOptionalJiraLink_NormalizesUrl()
    {
        var ok = RequestValidation.TryNormalizeOptionalJiraLink(" https://jira.example.com/browse/TIX-1 ", out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("https://jira.example.com/browse/TIX-1", normalized);
    }
}
