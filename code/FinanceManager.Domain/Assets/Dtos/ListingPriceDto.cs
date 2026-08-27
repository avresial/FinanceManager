namespace FinanceManager.Domain.Assets.Dtos;

public record ListingPriceDto(
    decimal? LatestPrice,
    string Currency,
    string? Message = null,
    DateTime? RetryAtUtc = null);