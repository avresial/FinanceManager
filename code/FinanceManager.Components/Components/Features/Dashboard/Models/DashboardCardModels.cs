using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Components.Features.Dashboard.Models;

// Card-specific read models the dashboard prepares from the single overview
// response and passes to the first-paint cards through explicit [Parameter]
// values. Each card applies its existing presentation transform to this data,
// so the smart cards can keep working standalone when no model is supplied.

/// <summary>Prepared data for the time-series cards (net worth, net cash flow, closing balance).</summary>
public record TimeSeriesCardModel(List<TimeSeriesModel> Series);

/// <summary>Prepared data for the distribution cards that toggle between a type and an account/wallet view (liabilities, assets).</summary>
public record DistributionCardModel(List<NameValueResult> TypeData, List<NameValueResult> AccountData);

/// <summary>Prepared data for the name/value list cards (financial labels, expense distribution).</summary>
public record NameValueListCardModel(List<NameValueResult> Items);