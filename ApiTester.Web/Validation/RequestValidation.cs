using ApiTester.McpServer.Persistence.Stores;

namespace ApiTester.Web.Validation;

public static class RequestValidation
{
    public static bool TryNormalizePageSize(
        int? pageSize,
        int? take,
        int defaultValue,
        int minValue,
        int maxValue,
        out int normalized,
        out string error)
    {
        var value = pageSize ?? take;

        if (!value.HasValue)
        {
            normalized = defaultValue;
            error = string.Empty;
            return true;
        }

        if (value.Value < minValue || value.Value > maxValue)
        {
            normalized = defaultValue;
            error = $"pageSize must be between {minValue} and {maxValue}.";
            return false;
        }

        normalized = value.Value;
        error = string.Empty;
        return true;
    }

    public static bool TryNormalizePageToken(string? pageToken, int? skip, out int normalized, out string error)
    {
        if (string.IsNullOrWhiteSpace(pageToken) && !skip.HasValue)
        {
            normalized = 0;
            error = string.Empty;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!int.TryParse(pageToken, out normalized) || normalized < 0)
            {
                error = "pageToken must be a non-negative integer.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (skip is null || skip.Value < 0)
        {
            normalized = 0;
            error = "skip must be a non-negative integer.";
            return false;
        }

        normalized = skip.Value;
        error = string.Empty;
        return true;
    }

    public static bool TryNormalizeSort(string? sort, SortField defaultValue, out SortField normalized, out string error)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            normalized = defaultValue;
            error = string.Empty;
            return true;
        }

        if (!Enum.TryParse<SortField>(sort, true, out normalized))
        {
            error = "sort must be createdUtc or startedUtc.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryNormalizeOrder(string? order, SortDirection defaultValue, out SortDirection normalized, out string error)
    {
        if (string.IsNullOrWhiteSpace(order))
        {
            normalized = defaultValue;
            error = string.Empty;
            return true;
        }

        if (!Enum.TryParse<SortDirection>(order, true, out normalized))
        {
            error = "order must be asc or desc.";
            return false;
        }

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
