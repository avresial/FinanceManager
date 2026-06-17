using CsvHelper.Configuration;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Components.DtoMapping;

public sealed class ImportStockExtendedModelMap : ClassMap<ImportStockExtendedModel>
{
    public ImportStockExtendedModelMap(string postingDateHeader, string valueChangeHeader, string tickerHeader, string investmentTypeHeader)
    {
        Map(m => m.PostingDate).Name(postingDateHeader);
        Map(m => m.ValueChange).Name(valueChangeHeader);
        Map(m => m.Ticker).Name(tickerHeader);
        Map(m => m.InvestmentType).Name(investmentTypeHeader);
    }
}