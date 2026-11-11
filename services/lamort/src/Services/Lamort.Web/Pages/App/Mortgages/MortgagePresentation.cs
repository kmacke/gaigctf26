using System.Globalization;
using Lamort.Web.Data.Mortgages;

namespace Lamort.Web.Pages.App.Mortgages;

public static class MortgagePresentation
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");
    private const string DefaultApplicationName = "Mortgage application";

    public static string FormatCurrency(decimal amount)
    {
        return string.Format(UsCulture, "{0:C0}", amount);
    }

    public static string FormatCurrency(decimal? amount)
    {
        return amount.HasValue ? FormatCurrency(amount.Value) : "Not provided";
    }

    public static string FormatEntryCount(int count)
    {
        return count == 0 ? "Not provided" : count == 1 ? "1 entry" : $"{count} entries";
    }

    public static string FormatHomePriceRange(MortgageApplication application)
    {
        if (application.HomePriceMin is null || application.HomePriceMax is null)
        {
            return "Not provided";
        }

        return $"{FormatCurrency(application.HomePriceMin.Value)} to {FormatCurrency(application.HomePriceMax.Value)}";
    }

    public static string FormatStatus(MortgageApplicationStatus status)
    {
        return status == MortgageApplicationStatus.ReadyForReview ? "Under review" : "In progress";
    }

    public static string FormatEvaluationStatus(MortgageEvaluation? evaluation)
    {
        if (evaluation is null)
        {
            return "Under review";
        }

        return evaluation.Approved ? "Approved" : "Denied";
    }

    public static string FormatApplicationName(MortgageApplication application)
    {
        return string.IsNullOrWhiteSpace(application.ApplicationName)
            ? DefaultApplicationName
            : application.ApplicationName.Trim();
    }
}
