namespace Lamort.Shared.Web;

public sealed class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
}
