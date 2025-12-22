namespace ApiTester.McpServer.Models;

public sealed class TestPlan
{
    public required string OperationId { get; init; }
    public required string Method { get; init; }
    public required string PathTemplate { get; init; }
    public List<TestCase> Cases { get; set; } = new();

}

public sealed class TestCase
{
    public required string Name { get; init; }

    // simple JSON-friendly shapes
    public Dictionary<string, string> PathParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> QueryParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // for now keep deterministic + simple, we won’t do bodies until we add POST support
    public string? BodyJson { get; init; }

    public List<int> ExpectedStatusCodes { get; init; } = new();
}

public sealed class TestRunResult
{
    public required string OperationId { get; init; }
    public required int TotalCases { get; init; }
    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required int Blocked { get; init; }
    public required long TotalDurationMs { get; init; }

    public List<TestCaseResult> Results { get; init; } = new();
}

public sealed class TestCaseResult
{
    public required string Name { get; init; }

    public bool Blocked { get; init; }
    public string? BlockReason { get; init; }

    public string? Url { get; init; }
    public string? Method { get; init; }

    public int? StatusCode { get; init; }
    public long DurationMs { get; init; }

    public bool Pass { get; init; }
    public string? FailureReason { get; init; }

    public string? ResponseSnippet { get; init; }
}
