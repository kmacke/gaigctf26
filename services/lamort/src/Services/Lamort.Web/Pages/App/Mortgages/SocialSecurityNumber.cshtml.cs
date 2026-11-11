using System.ComponentModel.DataAnnotations;
using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lamort.Web.Pages.App.Mortgages;

public sealed class SocialSecurityNumberModel(
    WebDbContext dbContext,
    UserManager<LamortUser> userManager) : MortgageWizardPageModel(dbContext, userManager)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var application = await LoadApplicationAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        if (IsSubmitted(application))
        {
            return RedirectToWizardPage("/App/Mortgages/Review", id);
        }

        Id = id;
        Input = new InputModel
        {
            SocialSecurityNumber = application.SocialSecurityNumber ?? string.Empty
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var application = await LoadApplicationAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        if (IsSubmitted(application))
        {
            return RedirectToWizardPage("/App/Mortgages/Review", id);
        }

        Id = id;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        application.SocialSecurityNumber = Input.SocialSecurityNumber;
        application.UpdatedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        return RedirectToWizardPage("/App/Mortgages/AssetsIncome", id);
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(128)]
        public string SocialSecurityNumber { get; set; } = string.Empty;
    }
}
