namespace Lamort.Web.Data.Mortgages;

public sealed class MortgageIncome
{
    public Guid Id { get; set; }

    public Guid MortgageApplicationId { get; set; }

    public MortgageApplication MortgageApplication { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string IncomeType { get; set; } = string.Empty;

    public string Amount { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
