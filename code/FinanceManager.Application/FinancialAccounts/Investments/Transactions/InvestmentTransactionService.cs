using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.Shared.Persistence;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Investments.Transactions;

public sealed class InvestmentTransactionService(
    IAssetListingRepository listingRepository,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentInstrumentSearchService instrumentSearchService,
    IInstrumentImportService instrumentImportService,
    IAtomicOperation atomicOperation,
    ICacheInvalidator cacheInvalidator,
    ILogger<InvestmentTransactionService> logger) : IInvestmentTransactionService
{
    private static readonly SemaphoreSlim _externalAddGate = new(1, 1);

    public async Task<InvestmentTransactionDto> AddAsync(
        AddInvestmentTransactionRequest request,
        long userId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var external = !string.IsNullOrWhiteSpace(request.ExternalInstrumentResultId);
        InstrumentDiscoveryResultDto? instrument = null;
        if (external)
        {
            instrument = await instrumentSearchService.ResolveExternalResultAsync(
                request.ExternalInstrumentResultId!, cancellationToken);
            if (instrument is null)
                throw new InvestmentTransactionCommandException(
                    "instrument_result_expired",
                    "The external search result expired. Search for the instrument again.");

            try
            {
                // Provider validation is deliberately completed before the database transaction starts.
                await instrumentImportService.ValidateForTransactionAsync(instrument, cancellationToken);
            }
            catch (InstrumentImportException ex)
            {
                throw new InvestmentTransactionCommandException(ex.Code, ex.Message, ex);
            }
        }
        else if (await listingRepository.Get(request.AssetListingId!.Value, cancellationToken) is null)
        {
            throw new InvestmentTransactionCommandException(
                "instrument_not_found",
                "The selected instrument no longer exists. Search for it again.");
        }

        var gateTaken = false;
        try
        {
            if (external)
            {
                // ponytail: process-wide gate; use a distributed lock if imports run across multiple API instances.
                await _externalAddGate.WaitAsync(cancellationToken);
                gateTaken = true;
            }

            var result = await atomicOperation.ExecuteAsync(async transactionCancellationToken =>
            {
                long listingId;
                if (instrument is not null)
                {
                    ImportedInstrumentDto imported;
                    try
                    {
                        imported = await instrumentImportService.ImportValidatedAsync(instrument, transactionCancellationToken);
                    }
                    catch (InstrumentImportException ex)
                    {
                        throw new InvestmentTransactionCommandException(ex.Code, ex.Message, ex);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        throw new InvestmentTransactionCommandException(
                            "instrument_import_failed",
                            "The instrument could not be imported. No asset or transaction was created.",
                            ex);
                    }

                    listingId = imported.AssetListingId;
                }
                else
                {
                    listingId = request.AssetListingId!.Value;
                }

                InvestmentTransactionDto result;
                try
                {
                    var transaction = await transactionRepository.Add(
                        request.ToEntity(userId, listingId),
                        transactionCancellationToken);
                    var saved = await transactionRepository.Get(transaction.Id, transactionCancellationToken);
                    if (saved is null)
                        throw new InvalidOperationException("The created transaction could not be reloaded.");

                    result = saved.ToDto();
                }
                catch (InvestmentTransactionCommandException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new InvestmentTransactionCommandException(
                        "transaction_creation_failed",
                        "The transaction could not be saved. No imported instrument data was kept.",
                        ex);
                }

                return result;
            }, cancellationToken);

            await cacheInvalidator.InvalidateUser((int)userId, cancellationToken);
            return result;
        }
        catch (InvestmentTransactionCommandException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to add investment transaction for account {AccountId}", request.AccountId);
            throw new InvestmentTransactionCommandException(
                external ? "instrument_import_failed" : "transaction_creation_failed",
                external
                    ? "The instrument could not be imported. No asset or transaction was created."
                    : "The transaction could not be saved.",
                ex);
        }
        finally
        {
            if (gateTaken)
                _externalAddGate.Release();
        }
    }

    private static void ValidateRequest(AddInvestmentTransactionRequest request)
    {
        var hasListing = request.AssetListingId is > 0;
        var hasExternal = !string.IsNullOrWhiteSpace(request.ExternalInstrumentResultId);
        if (hasListing == hasExternal || request.AssetListingId is 0)
            throw new InvestmentTransactionCommandException(
                "instrument_source_invalid",
                "Select exactly one existing or external instrument.");

        if (request.Quantity <= 0m || request.UnitPrice < 0m
            || string.IsNullOrWhiteSpace(request.Currency)
            || request.TradeDate == default)
            throw new InvestmentTransactionCommandException(
                "transaction_creation_failed",
                "Invalid transaction details.");
    }
}