namespace ApiTester.Web;

public static class OpenApiImportLimits
{
    public const int MaxSpecBytes = 16 * 1024 * 1024;
    public const int MaxRequestBodyBytes = MaxSpecBytes + 16_384;
}
