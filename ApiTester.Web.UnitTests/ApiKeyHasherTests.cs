using ApiTester.Web.Auth;

namespace ApiTester.Web.UnitTests;

public sealed class ApiKeyHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForMatchingKey()
    {
        var key = "alpha01.secret-token";
        var hash = ApiKeyHasher.Hash(key);

        Assert.True(ApiKeyHasher.Verify(key, hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMismatchedKey()
    {
        var hash = ApiKeyHasher.Hash("alpha01.secret-token");

        Assert.False(ApiKeyHasher.Verify("alpha01.other", hash));
    }
}
