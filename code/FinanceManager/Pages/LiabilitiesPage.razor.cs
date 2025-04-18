using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages;

public partial class LiabilitiesPage
{
    private const int _unitHeight = 190;

    public decimal TotalLiabilities;
    public DateTime StartDateTime { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
}