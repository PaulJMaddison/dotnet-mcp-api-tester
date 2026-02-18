using ApiTester.Cli;

namespace ApiTester.Cli.Tests;

public sealed class SecretRedactorTests
{
    [Theory]
    [InlineData(null, "(empty)")]
    [InlineData("", "(empty)")]
    [InlineData("abc", "***")]
    [InlineData("super-secret-token", "supe***en")]
    public void Redact_MasksExpectedValues(string? input, string expected)
    {
        var actual = SecretRedactor.Redact(input);
        Assert.Equal(expected, actual);
    }
}
