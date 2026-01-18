using ApiTester.McpServer.Models;

namespace ApiTester.Web.Auth;

public static class ApiKeyAccessEvaluator
{
    public static bool IsActive(ApiKeyRecord record, DateTime nowUtc)
    {
        if (record.RevokedUtc.HasValue && record.RevokedUtc.Value <= nowUtc)
            return false;

        if (record.ExpiresUtc.HasValue && record.ExpiresUtc.Value <= nowUtc)
            return false;

        return true;
    }
}
