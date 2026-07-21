using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FinanceManager.Api.Mcp;

[McpServerToolType]
public sealed class ReferenceDataTools(
    McpUserContext userContext,
    ICurrencyRepository currencies,
    IFinancialLabelsRepository labels)
{
    [McpServerTool(Name = "list_reference_data", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List safe currency and financial-label reference data available to the authenticated user.")]
    public async Task<McpReferenceData> ListReferenceData(CancellationToken cancellationToken = default)
    {
        _ = userContext.GetUserId();
        var currencyModels = await currencies.GetCurrencies(cancellationToken)
            .OrderBy(currency => currency.ShortName)
            .Select(McpResponseMapper.Currency)
            .ToListAsync(cancellationToken);
        var labelModels = await labels.GetLabels(cancellationToken)
            .OrderBy(label => label.Name)
            .Select(McpResponseMapper.Label)
            .ToListAsync(cancellationToken);
        return new McpReferenceData(currencyModels, labelModels);
    }
}