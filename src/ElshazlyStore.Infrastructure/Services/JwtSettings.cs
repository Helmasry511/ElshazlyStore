namespace ElshazlyStore.Infrastructure.Services;

/// <summary>
/// JWT configuration settings bound from appsettings.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ElshazlyStore";
    public string Audience { get; set; } = "ElshazlyStore.Desktop";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
