namespace FinanceManager.Domain.Commands.Stocks;

public sealed record UpdateStockRequest(string Ticker, string Name, string Type, string Region, string Currency);