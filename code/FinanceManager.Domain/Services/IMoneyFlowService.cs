﻿using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;
public interface IMoneyFlowService
{
    Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetIncome(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetSpending(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetBalance(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end);
    IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end);
}
