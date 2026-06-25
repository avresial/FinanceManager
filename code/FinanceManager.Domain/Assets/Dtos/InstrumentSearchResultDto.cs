namespace FinanceManager.Domain.Assets.Dtos;

public record InstrumentSearchResultDto(long ListingId, string Ticker, string ExchangeName, string Currency);