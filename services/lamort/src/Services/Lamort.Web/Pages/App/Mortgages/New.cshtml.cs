using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Lamort.Web.Data.Mortgages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lamort.Web.Pages.App.Mortgages;

[Authorize]
public sealed class NewModel(
    WebDbContext dbContext,
    UserManager<LamortUser> userManager) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var application = new MortgageApplication
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Status = MortgageApplicationStatus.Draft,
            DocumentsRequested = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.MortgageApplications.Add(application);
        await dbContext.SaveChangesAsync();

        return RedirectToPage("/App/Mortgages/CoApplicants", new { id = application.Id });
    }
}
