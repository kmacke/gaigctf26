using System.ComponentModel.DataAnnotations;
using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lamort.Web.Pages.App.Mortgages;

public sealed class HomePriceModel(
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
            HomePriceMin = application.HomePriceMin,
            HomePriceMax = application.HomePriceMax
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

        if (Input.HomePriceMax < Input.HomePriceMin)
        {
            ModelState.AddModelError(string.Empty, "The upper end of the home price range must be greater than or equal to the minimum.");
            return Page();
        }

        application.HomePriceMin = Input.HomePriceMin;
        application.HomePriceMax = Input.HomePriceMax;
        application.UpdatedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        return RedirectToWizardPage("/App/Mortgages/Review", id);
    }

    public sealed class InputModel
    {
        [Required]
        [Range(typeof(decimal), "0", "999999999999999.99")]
        public decimal? HomePriceMin { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "999999999999999.99")]
        public decimal? HomePriceMax { get; set; }
    }
}
