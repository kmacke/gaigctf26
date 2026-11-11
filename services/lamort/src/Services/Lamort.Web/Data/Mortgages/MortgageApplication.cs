namespace Lamort.Web.Data.Mortgages;

public sealed class MortgageApplication
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? ApplicationName { get; set; }

    public string? CoApplicantUserId { get; set; }

    public string? CoApplicantUserName { get; set; }

    public bool HasCoApplicants { get; set; }

    public string? SocialSecurityNumber { get; set; }

    public bool DocumentsRequested { get; set; }

    public decimal? HomePriceMin { get; set; }

    public decimal? HomePriceMax { get; set; }

    public MortgageApplicationStatus Status { get; set; } = MortgageApplicationStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public List<MortgageAsset> Assets { get; set; } = [];

    public List<MortgageIncome> Incomes { get; set; } = [];

    public List<MortgageDebt> Debts { get; set; } = [];
}
