namespace Lamort.Shared.Messaging;

public sealed class MessageCreatedEvent
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
