namespace Lamort.Shared.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "lamort-web";
    public string Audience { get; set; } = "lamort-internal";
    public string SigningKey { get; set; } = "change-this-local-dev-signing-key-12345";
    public int ExpirationMinutes { get; set; } = 60;
}
