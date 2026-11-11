namespace Lamort.Shared.Web;

public sealed class MessageDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
}
