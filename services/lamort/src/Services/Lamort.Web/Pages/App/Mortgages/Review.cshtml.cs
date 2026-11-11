using Lamort.Shared.Auth;
using Lamort.Shared.Messaging;
using Lamort.Web.Data;
using Lamort.Web.Data.Mortgages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lamort.Web.Pages.App.Mortgages;

public sealed class ReviewModel(
    WebDbContext dbContext,
    UserManager<LamortUser> userManager,
    IMessagePublisher publisher) : MortgageWizardPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public string ApplicationName { get; private set; } = string.Empty;

    public ReviewSummary Summary { get; private set; } = new();

    public bool ApplicationSubmitted { get; private set; }

    public EvaluationViewModel? Evaluation { get; private set; }

    public string HeroTitle =>
        !ApplicationSubmitted
            ? "Review your application"
            : Evaluation is null
                ? "Your application is under review"
                : Evaluation.Approved
                    ? "Your application was approved"
                    : "Your application was denied";

    public string HeroMessage =>
        !ApplicationSubmitted
            ? "Confirm the financial profile below before you send the application for review."
            : Evaluation is null
                ? "Your information has been submitted to the mortgage review team. We will keep the file visible in your application dashboard."
                : Evaluation.Reason;

    public string HeroStatus =>
        !ApplicationSubmitted
            ? Summary.Status
            : Evaluation is null
                ? "Under review"
                : Evaluation.Approved
                    ? "Approved"
                    : "Denied";

    public string HeroStatusClass =>
        !ApplicationSubmitted
            ? "status-draft"
            : "status-review";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var application = await LoadApplicationAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        Id = id;
        ApplicationName = MortgagePresentation.FormatApplicationName(application);
        ApplicationSubmitted = application.Status == MortgageApplicationStatus.ReadyForReview;
        Summary = ReviewSummary.FromApplication(application);
        Evaluation = await DbContext.MortgageEvaluations
            .AsNoTracking()
            .Where(x => x.MortgageApplicationId == id)
            .Select(x => new EvaluationViewModel
            {
                Approved = x.Approved,
                Reason = x.Reason,
                EvaluatedAt = x.EvaluatedAt
            })
            .FirstOrDefaultAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var application = await LoadApplicationAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        if (application.Status != MortgageApplicationStatus.ReadyForReview)
        {
            var submittedAt = DateTimeOffset.UtcNow;
            application.Status = MortgageApplicationStatus.ReadyForReview;
            application.SubmittedAt = submittedAt;
            application.UpdatedAt = submittedAt;
            await DbContext.SaveChangesAsync();

            await publisher.PublishAsync(new MortgageApplicationReadyForReviewEvent
            {
                MortgageApplicationId = application.Id,
                UserId = application.UserId,
                SubmittedAt = submittedAt
            });
        }

        return RedirectToPage(new { id });
    }

    public sealed class ReviewSummary
    {
        public string Status { get; init; } = string.Empty;

        public string CoApplicants { get; init; } = string.Empty;

        public string SocialSecurityNumber { get; init; } = string.Empty;

        public string AssetEntries { get; init; } = string.Empty;

        public string IncomeEntries { get; init; } = string.Empty;

        public string TotalDebts { get; init; } = string.Empty;

        public string HomePriceRange { get; init; } = string.Empty;

        public static ReviewSummary FromApplication(MortgageApplication application)
        {
            return new ReviewSummary
            {
                Status = MortgagePresentation.FormatStatus(application.Status),
                CoApplicants = application.HasCoApplicants ? "Yes" : "No",
                SocialSecurityNumber = string.IsNullOrWhiteSpace(application.SocialSecurityNumber) ? "Not provided" : application.SocialSecurityNumber,
                AssetEntries = MortgagePresentation.FormatEntryCount(application.Assets.Count),
                IncomeEntries = MortgagePresentation.FormatEntryCount(application.Incomes.Count),
                TotalDebts = MortgagePresentation.FormatCurrency(application.Debts.Sum(x => x.Amount)),
                HomePriceRange = MortgagePresentation.FormatHomePriceRange(application)
            };
        }
    }

    public sealed class EvaluationViewModel
    {
        public bool Approved { get; init; }

        public string Reason { get; init; } = string.Empty;

        public DateTimeOffset EvaluatedAt { get; init; }
    }
}
