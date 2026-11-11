using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Lamort.Web.Data.Mortgages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lamort.Web.Pages.App.Mortgages;

public sealed class DebtsModel(
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
            Debts = application.Debts
                .OrderBy(x => x.SortOrder)
                .Select(x => new MortgageDebtInput { Name = x.Name, Amount = x.Amount })
                .DefaultIfEmpty(new MortgageDebtInput())
                .ToList()
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
        Normalize(Input.Debts);

        if (!ModelState.IsValid)
        {
            EnsureDefaults();
            return Page();
        }

        DbContext.MortgageDebts.RemoveRange(application.Debts);
        var debts = Input.Debts
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Amount.HasValue)
            .Select((x, index) => new MortgageDebt
            {
                Id = Guid.NewGuid(),
                MortgageApplicationId = application.Id,
                Name = x.Name.Trim(),
                Amount = x.Amount!.Value,
                SortOrder = index
            })
            .ToList();

        DbContext.MortgageDebts.AddRange(debts);

        application.UpdatedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();
        return RedirectToWizardPage("/App/Mortgages/HomePrice", id);
    }

    private void EnsureDefaults()
    {
        Input.Debts = Input.Debts.Count == 0 ? [new MortgageDebtInput()] : Input.Debts;
    }

    private static void Normalize(List<MortgageDebtInput> debts)
    {
        debts.RemoveAll(x => string.IsNullOrWhiteSpace(x.Name) && !x.Amount.HasValue);
    }

    public sealed class InputModel
    {
        public List<MortgageDebtInput> Debts { get; set; } = [new()];
    }
}
