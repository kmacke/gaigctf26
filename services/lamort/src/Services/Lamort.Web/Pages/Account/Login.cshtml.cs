using System.ComponentModel.DataAnnotations;
using Lamort.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lamort.Web.Pages.Account;

public sealed class LoginModel(
    SignInManager<LamortUser> signInManager,
    UserManager<LamortUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var credential = Input.Credential.Trim();
        LamortUser? user = null;

        if (!string.IsNullOrWhiteSpace(credential))
        {
            user = await userManager.FindByEmailAsync(credential)
                ?? await userManager.FindByNameAsync(credential);
        }

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login or password.");
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            return RedirectToPage("/App/Index");
        }

        ModelState.AddModelError(string.Empty, "Invalid login or password.");
        return Page();
    }

    public sealed class InputModel
    {
        [Required]
        public string Credential { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
