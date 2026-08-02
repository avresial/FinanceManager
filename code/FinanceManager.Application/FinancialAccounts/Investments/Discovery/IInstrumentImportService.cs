using FinanceManager.Domain.Assets.Discovery;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public interface IInstrumentImportService
{
    Task<InstrumentImportPreviewDto> GetImportPreviewAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
    Task<ImportedInstrumentDto> ImportAsync(ImportInstrumentCommand command, CancellationToken ct = default);
    /// Strictly validate provider data before starting the transaction's database operation.
    Task ValidateForTransactionAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);

    /// Revalidates the provider symbol defensively, then persists the already validated instrument.
    Task<ImportedInstrumentDto> ImportValidatedAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
}