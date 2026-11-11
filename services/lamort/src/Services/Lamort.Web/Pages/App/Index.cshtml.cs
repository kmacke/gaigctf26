using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Lamort.Web.Data.Mortgages;
using Lamort.Web.Pages.App.Mortgages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;

namespace Lamort.Web.Pages.App;

[Authorize]
public sealed class IndexModel(
    UserManager<LamortUser> userManager,
    WebDbContext dbContext,
    Lamort.Web.Services.JwtTokenService tokenService) : PageModel
{
    public string FirstName { get; private set; } = "there";

    public string SharedApplicationsUrl { get; private set; } = "/coop/";

    public IReadOnlyList<ApplicationSummary> Applications { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        FirstName = GetFirstName(user);

        if (user is null)
        {
            Applications = [];
            return;
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            // SECURITY: signed, audience-scoped, 15 minute token (was an unauthenticated base64 username).
            var coopToken = tokenService.CreateToken(user, Lamort.Shared.Auth.JwtSettings.CoopAudience, lifetimeMinutes: 15);
            SharedApplicationsUrl = $"/coop/?token={Uri.EscapeDataString(coopToken.AccessToken)}";
        }

        var applications = await dbContext.MortgageApplications
            .Where(x => x.UserId == user.Id)
            .Include(x => x.Assets)
            .Include(x => x.Incomes)
            .Include(x => x.Debts)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var renderedMortgageIds = (await dbContext.RenderedMortgagePdfs
            .Where(x => x.UserId == user.Id)
            .Select(x => x.MortgageApplicationId)
            .ToListAsync())
            .ToHashSet();

        var applicationIds = applications.Select(x => x.Id).ToList();
        var evaluations = await dbContext.MortgageEvaluations
            .AsNoTracking()
            .Where(x => applicationIds.Contains(x.MortgageApplicationId))
            .ToDictionaryAsync(x => x.MortgageApplicationId);

        Applications = applications
            .Select(x => new ApplicationSummary
            {
                Id = x.Id,
                ApplicationName = MortgagePresentation.FormatApplicationName(x),
                IsSubmitted = x.Status == MortgageApplicationStatus.ReadyForReview,
                HasRenderedPdf = renderedMortgageIds.Contains(x.Id),
                Status = x.Status == MortgageApplicationStatus.ReadyForReview
                    ? MortgagePresentation.FormatEvaluationStatus(evaluations.GetValueOrDefault(x.Id))
                    : MortgagePresentation.FormatStatus(x.Status),
                EvaluationReason = evaluations.GetValueOrDefault(x.Id)?.Reason,
                CreatedAt = x.CreatedAt,
                CoApplicants = x.HasCoApplicants ? "Yes" : "No",
                AssetEntries = MortgagePresentation.FormatEntryCount(x.Assets.Count),
                IncomeEntries = MortgagePresentation.FormatEntryCount(x.Incomes.Count),
                TotalDebts = MortgagePresentation.FormatCurrency(x.Debts.Sum(a => a.Amount)),
                HomePriceRange = MortgagePresentation.FormatHomePriceRange(x)
            })
            .ToList();
    }

    private static string GetFirstName(LamortUser? user)
    {
        if (user is null)
        {
            return "there";
        }

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            var firstName = user.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                return firstName;
            }
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
        }

        return user.UserName ?? "there";
    }

    public sealed class ApplicationSummary
    {
        public Guid Id { get; init; }

        public string ApplicationName { get; init; } = string.Empty;

        public bool IsSubmitted { get; init; }

        public bool HasRenderedPdf { get; init; }

        public string Status { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public string? EvaluationReason { get; init; }

        public string CoApplicants { get; init; } = string.Empty;

        public string AssetEntries { get; init; } = string.Empty;

        public string IncomeEntries { get; init; } = string.Empty;

        public string TotalDebts { get; init; } = string.Empty;

        public string HomePriceRange { get; init; } = string.Empty;
    }
}
