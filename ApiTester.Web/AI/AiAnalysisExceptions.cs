namespace ApiTester.Web.AI;

public sealed class AiSchemaValidationException : Exception
{
    public AiSchemaValidationException(string message) : base(message) { }
}

public sealed class AiRateLimitExceededException : Exception
{
    public AiRateLimitExceededException(string message) : base(message) { }
}

public sealed class AiFeatureDisabledException : Exception
{
    public AiFeatureDisabledException(string message) : base(message) { }
}
