namespace ApiTester.Web.Validation;

public static class RequestValidation
{
    public static bool TryNormalizeTake(int? take, int defaultValue, int minValue, int maxValue, out int normalized, out string error)
    {
        if (!take.HasValue)
        {
            normalized = defaultValue;
            error = string.Empty;
            return true;
        }

        if (take.Value < minValue || take.Value > maxValue)
        {
            normalized = defaultValue;
            error = $"take must be between {minValue} and {maxValue}.";
            return false;
        }

        normalized = take.Value;
        error = string.Empty;
        return true;
    }

    public static bool TryValidateRequiredName(string? name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Name is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateRequiredKey(string? key, string fieldName, out string error)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            error = $"{fieldName} is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryNormalizeOptionalValue(string? value, out string? normalized, out string error)
    {
        if (value is null)
        {
            normalized = null;
            error = string.Empty;
            return true;
        }

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            normalized = null;
            error = "Value cannot be empty.";
            return false;
        }

        normalized = trimmed;
        error = string.Empty;
        return true;
    }

    public static bool TryParseGuid(string? raw, out Guid parsed, out string error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            parsed = Guid.Empty;
            error = "Id is required.";
            return false;
        }

        if (!Guid.TryParse(raw, out parsed))
        {
            error = "Invalid GUID format.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
