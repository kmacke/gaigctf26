namespace Lamort.Shared.Messaging;

public sealed class MortgageApplicationReadyForReviewEvent
{
    public Guid MortgageApplicationId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; set; }
}
