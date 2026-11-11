namespace Lamort.Web.Data.Mortgages;

public sealed class MortgageDebt
{
    public Guid Id { get; set; }

    public Guid MortgageApplicationId { get; set; }

    public MortgageApplication MortgageApplication { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int SortOrder { get; set; }
}
