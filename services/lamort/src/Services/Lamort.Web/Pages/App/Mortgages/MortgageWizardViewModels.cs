using System.ComponentModel.DataAnnotations;

namespace Lamort.Web.Pages.App.Mortgages;

public static class MortgageAmount
{
    public static bool IsValid(string? value)
    {
        return decimal.TryParse(
            value?.Trim().Replace("$", "").Replace(",", ""),
            System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) && Math.Abs(parsed) <= 999_999_999_999m;
    }
}

public sealed class MortgageAssetInput
{
    [Display(Name = "Asset name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Amount")]
    [StringLength(100)]
    public string Amount { get; set; } = string.Empty;
}

public sealed class MortgageIncomeInput
{
    [Display(Name = "Income source")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Income type")]
    [StringLength(100)]
    public string IncomeType { get; set; } = string.Empty;

    [Display(Name = "Amount")]
    [StringLength(100)]
    public string Amount { get; set; } = string.Empty;
}

public sealed class MortgageDebtInput
{
    [Display(Name = "Debt name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999999.99")]
    [Display(Name = "Amount")]
    public decimal? Amount { get; set; }
}
