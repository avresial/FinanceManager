using FinanceManager.Domain.Assets.Discovery;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public interface IInstrumentImportService
{
    Task<InstrumentImportPreviewDto> GetImportPreviewAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
    Task<ImportedInstrumentDto> ImportAsync(ImportInstrumentCommand command, CancellationToken ct = default);
    Task ValidateForTransactionAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
    Task<ImportedInstrumentDto> ImportValidatedAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
}