using FinanceManager.Domain.Assets.Discovery;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public interface IInstrumentImportService
{
    Task<InstrumentImportPreviewDto> GetImportPreviewAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
    Task<ImportedInstrumentDto> ImportAsync(ImportInstrumentCommand command, CancellationToken ct = default);

    /// <summary>
    /// Strictly validates provider data (structure plus configured-provider price checks) before the
    /// transaction's database operation starts. Callers must invoke this before
    /// <see cref="ImportValidatedAsync"/>.
    /// </summary>
    Task<ValidatedInstrumentImport> ValidateForTransactionAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);

    /// <summary>
    /// Persists an instrument whose provider symbol was already validated by
    /// <see cref="ValidateForTransactionAsync"/>. Only cheap structural checks run here, so no
    /// provider request is made inside the database transaction. Callers must call
    /// <see cref="ValidateForTransactionAsync"/> first.
    /// </summary>
    Task<ImportedInstrumentDto> ImportValidatedAsync(ValidatedInstrumentImport validated, CancellationToken ct = default);

    /// <summary>
    /// Pure structural check: returns whether <see cref="ValidateForTransactionAsync"/> would accept
    /// the instrument, without any provider request. Used to hide results that can never be saved.
    /// </summary>
    bool CanImportForTransaction(InstrumentDiscoveryResultDto instrument);
}