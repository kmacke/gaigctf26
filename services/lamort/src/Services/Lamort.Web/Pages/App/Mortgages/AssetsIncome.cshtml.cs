using Lamort.Shared.Auth;
using Lamort.Web.Data;
using Lamort.Web.Data.Mortgages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lamort.Web.Pages.App.Mortgages;

public sealed class AssetsIncomeModel(
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
            Assets = application.Assets
                .OrderBy(x => x.SortOrder)
                .Select(x => new MortgageAssetInput { Name = x.Name, Amount = x.Amount })
                .DefaultIfEmpty(new MortgageAssetInput())
                .ToList(),
            Incomes = application.Incomes
                .OrderBy(x => x.SortOrder)
                .Select(x => new MortgageIncomeInput { Name = x.Name, IncomeType = x.IncomeType, Amount = x.Amount })
                .DefaultIfEmpty(new MortgageIncomeInput())
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
        Normalize(Input.Assets, Input.Incomes);

        if (!HasAnyAssetOrIncome())
        {
            ModelState.AddModelError(string.Empty, "Add at least one asset or income source to continue.");
        }

        if (!ModelState.IsValid)
        {
            EnsureDefaults();
            return Page();
        }

        DbContext.MortgageAssets.RemoveRange(application.Assets);
        DbContext.MortgageIncomes.RemoveRange(application.Incomes);

        var assets = Input.Assets
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Amount))
            .Select((x, index) => new MortgageAsset
            {
                Id = Guid.NewGuid(),
                MortgageApplicationId = application.Id,
                Name = x.Name.Trim(),
                Amount = x.Amount.Trim(),
                SortOrder = index
            })
            .ToList();

        var incomes = Input.Incomes
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.IncomeType) && !string.IsNullOrWhiteSpace(x.Amount))
            .Select((x, index) => new MortgageIncome
            {
                Id = Guid.NewGuid(),
                MortgageApplicationId = application.Id,
                Name = x.Name.Trim(),
                IncomeType = x.IncomeType.Trim(),
                Amount = x.Amount.Trim(),
                SortOrder = index
            })
            .ToList();

        DbContext.MortgageAssets.AddRange(assets);
        DbContext.MortgageIncomes.AddRange(incomes);

        application.DocumentsRequested = true;
        application.UpdatedAt = DateTimeOffset.UtcNow;

        await DbContext.SaveChangesAsync();
        return RedirectToWizardPage("/App/Mortgages/Debts", id);
    }

    private bool HasAnyAssetOrIncome()
    {
        return Input.Assets.Any(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Amount))
            || Input.Incomes.Any(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.IncomeType) && !string.IsNullOrWhiteSpace(x.Amount));
    }

    private void EnsureDefaults()
    {
        Input.Assets = Input.Assets.Count == 0 ? [new MortgageAssetInput()] : Input.Assets;
        Input.Incomes = Input.Incomes.Count == 0 ? [new MortgageIncomeInput()] : Input.Incomes;
    }

    private static void Normalize(List<MortgageAssetInput> assets, List<MortgageIncomeInput> incomes)
    {
        assets.RemoveAll(x => string.IsNullOrWhiteSpace(x.Name) && string.IsNullOrWhiteSpace(x.Amount));
        incomes.RemoveAll(x => string.IsNullOrWhiteSpace(x.Name) && string.IsNullOrWhiteSpace(x.IncomeType) && string.IsNullOrWhiteSpace(x.Amount));
    }

    public sealed class InputModel
    {
        public List<MortgageAssetInput> Assets { get; set; } = [new()];

        public List<MortgageIncomeInput> Incomes { get; set; } = [new()];
    }
}
