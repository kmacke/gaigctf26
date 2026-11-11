using System.ComponentModel.DataAnnotations;
using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lamort.Web.Pages.App.Mortgages;

public sealed class CoApplicantsModel(
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
        Input.ApplicationName = application.ApplicationName;
        Input.CoApplicantUserName = application.CoApplicantUserName;
        Input.HasCoApplicants = application.HasCoApplicants;
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

        application.ApplicationName = string.IsNullOrWhiteSpace(Input.ApplicationName) ? null : Input.ApplicationName.Trim();
        application.HasCoApplicants = Input.HasCoApplicants;

        if (Input.HasCoApplicants)
        {
            var userName = Input.CoApplicantUserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                ModelState.AddModelError(nameof(Input.CoApplicantUserName), "Enter the username of the co-applicant.");
                return Page();
            }

            var coApplicant = await UserManager.FindByNameAsync(userName);
            if (coApplicant is null)
            {
                ModelState.AddModelError(nameof(Input.CoApplicantUserName), "No user exists with that username.");
                return Page();
            }

            application.CoApplicantUserId = coApplicant.Id;
            application.CoApplicantUserName = coApplicant.UserName;
        }
        else
        {
            application.CoApplicantUserId = null;
            application.CoApplicantUserName = null;
        }

        application.UpdatedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        return RedirectToWizardPage("/App/Mortgages/SocialSecurityNumber", id);
    }

    public sealed class InputModel
    {
        [Display(Name = "Application name")]
        [StringLength(200)]
        public string? ApplicationName { get; set; }

        [Display(Name = "Co-applicant username")]
        [StringLength(256)]
        public string? CoApplicantUserName { get; set; }

        [Display(Name = "Include a co-applicant")]
        public bool HasCoApplicants { get; set; }
    }
}
