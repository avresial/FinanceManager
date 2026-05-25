namespace FinanceManager.Domain.Commands.Stocks;

public sealed record AddStockRequest(string Ticker, string Name, string Type, string Region, string Currency);