using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class PortfolioReturnCard
{
    [Parameter] public string? Height { get; set; }
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; }
    [Parameter] public PortfolioReturnCardModel? Model { get; set; }
    [Parameter] public Currency Currency { get; set; } = DefaultCurrency.PLN;
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasError { get; set; }
    [Parameter] public EventCallback Retry { get; set; }

    private MoneyWeightedReturnResult? MoneyWeightedReturn => Model?.MoneyWeightedStatus is MoneyWeightedReturnStatus status ? new(Model.AnnualizedReturn, status, StartDateTime, EndDateTime) : null;
    private TimeWeightedReturnResult? TimeWeightedReturn => Model?.TimeWeightedStatus is TimeWeightedReturnStatus status ? new(Model.TotalReturn, status, StartDateTime, EndDateTime) : null;

    internal static string FormatReturn(decimal value) => $"{value * 100m:0.00}%";

    internal string RangeText => DateFormatter.FormatRange(StartDateTime, EndDateTime);
    internal int PeriodLength => Math.Max(0, (EndDateTime.Date - StartDateTime.Date).Days + 1);
    internal bool HasAvailableReturn => MoneyWeightedReturn?.IsAvailable == true || TimeWeightedReturn?.IsAvailable == true;
    internal decimal? ExternalCashMovement => Model?.ExternalCashMovement;

    internal string InsightText => "XIRR reflects how much you invested and when, as a yearly rate, not your gain for the selected period. "
        + "TWR neutralizes external cash-flow timing for this period. "
        + "Large flows can make signs differ. Short ranges can magnify annualized XIRR.";

    internal string MoneyWeightedStatusLabel => MoneyWeightedReturn?.Status switch
    {
        MoneyWeightedReturnStatus.InsufficientData => "Insufficient data",
        MoneyWeightedReturnStatus.NoSolution => "No solution",
        _ => "Unavailable",
    };

    internal string MoneyWeightedStatusText => MoneyWeightedReturn?.Status switch
    {
        MoneyWeightedReturnStatus.InsufficientData => "Not enough investment history for this range.",
        MoneyWeightedReturnStatus.NoSolution => "No valid annualized return solution for this range.",
        MoneyWeightedReturnStatus.Unavailable => "Prices or exchange rates are missing.",
        _ => "Could not load this return.",
    };

    internal string TimeWeightedStatusLabel => TimeWeightedReturn?.Status switch
    {
        TimeWeightedReturnStatus.InsufficientData => "Insufficient data",
        _ => "Unavailable",
    };

    internal string TimeWeightedStatusText => TimeWeightedReturn?.Status switch
    {
        TimeWeightedReturnStatus.InsufficientData => "Not enough portfolio history for this range.",
        TimeWeightedReturnStatus.Unavailable => "Prices or exchange rates are missing.",
        _ => "Could not load this return.",
    };

    private string FormatAmount(decimal value) => MoneyFormatter.Format(value, Currency.ShortName);
}