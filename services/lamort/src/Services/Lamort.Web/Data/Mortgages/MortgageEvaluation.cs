namespace Lamort.Web.Data.Mortgages;

public sealed class MortgageEvaluation
{
    public Guid MortgageApplicationId { get; set; }

    public bool Approved { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset EvaluatedAt { get; set; }
}
