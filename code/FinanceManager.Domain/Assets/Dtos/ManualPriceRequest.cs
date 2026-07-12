using System;

namespace FinanceManager.Domain.Assets.Dtos;

/// <summary>
/// Request model for submitting a manual price quote for an asset listing.
/// </summary>
public record ManualPriceRequest(decimal Price, DateTimeOffset Date);