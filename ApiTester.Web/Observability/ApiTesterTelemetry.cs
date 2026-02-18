using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiTester.Web.Observability;

public sealed class ApiTesterTelemetry : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _runsExecuted;
    private readonly Counter<long> _aiCalls;
    private readonly Counter<long> _quotaBlocks;
    private readonly Counter<long> _exportsGenerated;

    public static readonly ActivitySource ActivitySource = new("ApiTester.Web");

    public ApiTesterTelemetry()
    {
        _meter = new Meter("ApiTester.Web", "1.0.0");
        _runsExecuted = _meter.CreateCounter<long>("apitester_runs_executed_total", description: "Total test runs executed.");
        _aiCalls = _meter.CreateCounter<long>("apitester_ai_calls_total", description: "Total AI calls executed.");
        _quotaBlocks = _meter.CreateCounter<long>("apitester_quota_blocks_total", description: "Total quota/plan blocks returned.");
        _exportsGenerated = _meter.CreateCounter<long>("apitester_exports_generated_total", description: "Total report exports generated.");
    }

    public void RecordRunExecuted(string source)
        => _runsExecuted.Add(1, KeyValuePair.Create<string, object?>("source", source));

    public void RecordAiCall(string provider, string model)
        => _aiCalls.Add(1,
            KeyValuePair.Create<string, object?>("provider", provider),
            KeyValuePair.Create<string, object?>("model", model));

    public void RecordQuotaBlock(string gate)
        => _quotaBlocks.Add(1, KeyValuePair.Create<string, object?>("gate", gate));

    public void RecordExportGenerated(string format)
        => _exportsGenerated.Add(1, KeyValuePair.Create<string, object?>("format", format));

    public void Dispose()
    {
        _meter.Dispose();
        ActivitySource.Dispose();
    }
}
