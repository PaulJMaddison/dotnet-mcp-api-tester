using System.Globalization;

namespace ApiTester.Cli;

public static class CliParser
{
    public static bool TryParse(string[] args, out CliOptions? options, out string? error)
    {
        options = null;
        error = null;

        var remaining = new List<string>(args);

        var baseUrlValue = ReadOption(remaining, "--base-url") ?? Environment.GetEnvironmentVariable(CliOptions.BaseUrlEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrlValue) || !Uri.TryCreate(baseUrlValue, UriKind.Absolute, out var baseUrl))
        {
            error = $"A valid --base-url is required (or env {CliOptions.BaseUrlEnvVar}).";
            return false;
        }

        var token = ReadOption(remaining, "--token") ?? Environment.GetEnvironmentVariable(CliOptions.TokenEnvVar);
        if (string.IsNullOrWhiteSpace(token))
        {
            error = $"A --token is required (or env {CliOptions.TokenEnvVar}).";
            return false;
        }

        if (remaining.Count < 2)
        {
            error = Usage();
            return false;
        }

        CliCommand? command = null;
        if (Matches(remaining, "projects", "list"))
        {
            command = new CliCommand.ProjectsList();
        }
        else if (Matches(remaining, "run", "execute"))
        {
            var projectText = ReadOption(remaining, "--project", required: true);
            var operation = ReadOption(remaining, "--operation", required: true);

            if (!Guid.TryParse(projectText, out var projectId))
            {
                error = "--project must be a valid GUID.";
                return false;
            }

            command = new CliCommand.RunExecute(projectId, operation!);
        }
        else if (Matches(remaining, "run", "report"))
        {
            var runText = ReadOption(remaining, "--run", required: true);
            var format = ReadOption(remaining, "--format", required: true);

            if (!Guid.TryParse(runText, out var runId))
            {
                error = "--run must be a valid GUID.";
                return false;
            }

            format = format!.ToLower(CultureInfo.InvariantCulture);
            if (format is not ("md" or "json"))
            {
                error = "--format must be 'md' or 'json'.";
                return false;
            }

            command = new CliCommand.RunReport(runId, format);
        }
        else if (Matches(remaining, "run", "evidence-pack"))
        {
            var runText = ReadOption(remaining, "--run", required: true);
            var output = ReadOption(remaining, "--out", required: true);

            if (!Guid.TryParse(runText, out var runId))
            {
                error = "--run must be a valid GUID.";
                return false;
            }

            command = new CliCommand.RunEvidencePack(runId, output!);
        }

        if (command is null)
        {
            error = Usage();
            return false;
        }

        options = new CliOptions(baseUrl, token, command);
        return true;
    }

    public static string Usage() => "Usage: apitester [--base-url <url>] [--token <token>] projects list | run execute --project <id> --operation <opId> | run report --run <id> --format md|json | run evidence-pack --run <id> --out <path>";

    private static bool Matches(List<string> args, string first, string second)
    {
        return args.Count >= 2 &&
               string.Equals(args[0], first, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(args[1], second, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadOption(List<string> args, string name, bool required = false)
    {
        var index = args.FindIndex(v => string.Equals(v, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            if (required)
            {
                throw new InvalidOperationException($"Missing required option: {name}");
            }

            return null;
        }

        if (index == args.Count - 1)
        {
            throw new InvalidOperationException($"Option value is required: {name}");
        }

        var value = args[index + 1];
        args.RemoveAt(index + 1);
        args.RemoveAt(index);
        return value;
    }
}
