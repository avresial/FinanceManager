using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages;
public partial class LliabilitiesPage
{
    private const int _unitHeight = 190;

    public decimal TotalLiabilities;
    public DateTime StartDateTime { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<LliabilitiesPage> Logger { get; set; }



    protected override async Task OnInitializedAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        IEnumerable<BankAccount> bankAccounts = [];

        try
        {
            bankAccounts = await FinancialAccountService.GetAccounts<BankAccount>(user.UserId, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve bank accounts");
        }

        if (bankAccounts is null) return;

        bankAccounts = bankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value <= 0).ToList();
        TotalLiabilities = bankAccounts.Sum(x => x.Entries!.OrderByDescending(x => x.PostingDate).First().Value);
    }
}