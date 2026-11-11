namespace Lamort.Shared.Auth;

public sealed class JwtSettings
{
    public const string CoopAudience = "lamort-coop";

    public string Issuer { get; set; } = "lamort-web";
    public string Audience { get; set; } = "lamort-internal";

    // SECURITY: no default. The key must be supplied via secret/environment and be >= 32 bytes.
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || System.Text.Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 bytes long.");
        }
    }
}
