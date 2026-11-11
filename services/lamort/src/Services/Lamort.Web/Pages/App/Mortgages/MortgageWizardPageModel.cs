using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Lamort.Web.Data.Mortgages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Lamort.Web.Pages.App.Mortgages;

[Authorize]
public abstract class MortgageWizardPageModel(
    WebDbContext dbContext,
    UserManager<LamortUser> userManager) : PageModel
{
    protected WebDbContext DbContext { get; } = dbContext;

    protected UserManager<LamortUser> UserManager { get; } = userManager;

    protected async Task<MortgageApplication?> LoadApplicationAsync(Guid id)
    {
        // SECURITY: scope every lookup to the signed-in owner (prevents IDOR on read/edit/submit).
        var userId = UserManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await DbContext.MortgageApplications
            .Include(x => x.Assets)
            .Include(x => x.Incomes)
            .Include(x => x.Debts)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    protected IActionResult RedirectToWizardPage(string pageName, Guid id)
    {
        return RedirectToPage(pageName, new { id });
    }

    protected static bool IsSubmitted(MortgageApplication application)
    {
        return application.Status == MortgageApplicationStatus.ReadyForReview;
    }
}
