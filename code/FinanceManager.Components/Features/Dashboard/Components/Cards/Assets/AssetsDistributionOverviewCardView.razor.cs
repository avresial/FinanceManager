using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class AssetsDistributionOverviewCardView
{
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool HasError { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";
    [Parameter] public List<NameValueResult> TypeData { get; set; } = [];
    [Parameter] public List<NameValueResult> WalletData { get; set; } = [];
}