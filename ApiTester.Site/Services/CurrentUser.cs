using System.Security.Claims;

namespace ApiTester.Site.Services;

public interface ICurrentUser
{
    string UserId { get; }
    string Email { get; }
    string DisplayName { get; }
}

public sealed class ClaimsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId => GetValue(ClaimTypes.NameIdentifier, "sub");
    public string Email => GetValue(ClaimTypes.Email, "email");
    public string DisplayName => GetValue("name", ClaimTypes.Name, ClaimTypes.Email, "email");

    private string GetValue(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return string.Empty;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
