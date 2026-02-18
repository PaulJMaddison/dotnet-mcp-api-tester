namespace ApiTester.Cli;

public abstract record CliCommand
{
    private CliCommand() { }

    public sealed record ProjectsList : CliCommand;

    public sealed record RunExecute(Guid ProjectId, string OperationId) : CliCommand;

    public sealed record RunReport(Guid RunId, string Format) : CliCommand;

    public sealed record RunEvidencePack(Guid RunId, string OutputPath) : CliCommand;
}
