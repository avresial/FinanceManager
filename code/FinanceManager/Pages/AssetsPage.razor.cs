using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages;

public partial class AssetsPage
{
    private const int _unitHeight = 190;

    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

}