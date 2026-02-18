using ApiTester.Cli;

namespace ApiTester.Cli.Tests;

public sealed class CliParserTests
{
    [Fact]
    public void TryParse_ProjectsList_WithExplicitBaseUrlAndToken_Succeeds()
    {
        var args = new[] { "--base-url", "https://example.test", "--token", "abc12345", "projects", "list" };

        var ok = CliParser.TryParse(args, out var options, out var error);

        Assert.True(ok, error);
        Assert.NotNull(options);
        Assert.IsType<CliCommand.ProjectsList>(options!.Command);
        Assert.Equal("abc1***45", options.RedactedToken);
    }

    [Fact]
    public void TryParse_RunReport_WithInvalidFormat_Fails()
    {
        var args = new[]
        {
            "--base-url", "https://example.test", "--token", "abc12345", "run", "report", "--run", Guid.NewGuid().ToString(), "--format", "html"
        };

        var ok = CliParser.TryParse(args, out _, out var error);

        Assert.False(ok);
        Assert.Equal("--format must be 'md' or 'json'.", error);
    }

    [Fact]
    public void TryParse_UsesEnvironmentVariables_WhenFlagsNotProvided()
    {
        var baseUrlName = CliOptions.BaseUrlEnvVar;
        var tokenName = CliOptions.TokenEnvVar;
        var originalBaseUrl = Environment.GetEnvironmentVariable(baseUrlName);
        var originalToken = Environment.GetEnvironmentVariable(tokenName);

        try
        {
            Environment.SetEnvironmentVariable(baseUrlName, "https://env.example");
            Environment.SetEnvironmentVariable(tokenName, "secret-token");

            var ok = CliParser.TryParse(["projects", "list"], out var options, out var error);

            Assert.True(ok, error);
            Assert.NotNull(options);
            Assert.Equal(new Uri("https://env.example"), options!.BaseUrl);
            Assert.Equal("secret-token", options.Token);
        }
        finally
        {
            Environment.SetEnvironmentVariable(baseUrlName, originalBaseUrl);
            Environment.SetEnvironmentVariable(tokenName, originalToken);
        }
    }
}
