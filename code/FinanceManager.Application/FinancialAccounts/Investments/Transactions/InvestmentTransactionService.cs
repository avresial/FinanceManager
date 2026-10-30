using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.Shared.Persistence;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinanceManager.Application.FinancialAccounts.Investments.Transactions;

public sealed class InvestmentTransactionService(
    IAssetListingRepository listingRepository,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentInstrumentSearchService instrumentSearchService,
    IInstrumentImportService instrumentImportService,
    IAtomicOperation atomicOperation,
    ICacheInvalidator cacheInvalidator,
    ICurrencyRepository currencyRepository,
    ILogger<InvestmentTransactionService> logger) : IInvestmentTransactionService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _externalAddGates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<InvestmentTransactionDto> AddAsync(
        AddInvestmentTransactionRequest request,
        long userId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var normalizedCurrency = request.Currency.Trim().ToUpperInvariant();
        request = request with { Currency = normalizedCurrency };

        var external = !string.IsNullOrWhiteSpace(request.ExternalInstrumentResultId);
        InstrumentDiscoveryResultDto? instrument = null;
        ValidatedInstrumentImport? validated = null;
        if (external)
        {
            instrument = await instrumentSearchService.ResolveExternalResultAsync(
                request.ExternalInstrumentResultId!, cancellationToken);
            if (instrument is null)
                throw new InvestmentTransactionCommandException(
                    "instrument_result_expired",
                    "The external search result expired. Search for the instrument again.");

            await ValidateTransactionCurrencyAsync(instrument.TradingCurrency, normalizedCurrency, cancellationToken);

            try
            {
                // Provider validation is deliberately completed before the database transaction starts.
                validated = await instrumentImportService.ValidateForTransactionAsync(instrument, cancellationToken);
            }
            catch (InstrumentImportException ex)
            {
                throw new InvestmentTransactionCommandException(ex.Code, ex.Message, ex);
            }
        }
        else
        {
            var existingListing = await listingRepository.Get(request.AssetListingId!.Value, cancellationToken);
            if (existingListing is null)
            {
                throw new InvestmentTransactionCommandException(
                    "instrument_not_found",
                    "The selected instrument no longer exists. Search for it again.");
            }

            await ValidateTransactionCurrencyAsync(existingListing.TradingCurrency, normalizedCurrency, cancellationToken);
        }

        var externalGate = external
            ? _externalAddGates.GetOrAdd(GetExternalGateKey(instrument!), static _ => new(1, 1))
            : null;
        var gateTaken = false;
        try
        {
            if (externalGate is not null)
            {
                // ponytail: process-local keyed gates plus database upserts; use a distributed lock if throughput or deployment scale requires it.
                await externalGate.WaitAsync(cancellationToken);
                gateTaken = true;
            }

            var result = await atomicOperation.ExecuteAsync(async transactionCancellationToken =>
            {
                long listingId;
                bool? importedHasAutomaticPrices = null;
                if (instrument is not null)
                {
                    ImportedInstrumentDto imported;
                    try
                    {
                        imported = await instrumentImportService.ImportValidatedAsync(validated!, transactionCancellationToken);
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
                    importedHasAutomaticPrices = imported.MarketDataSymbolId is not null
                        && imported.ProviderResults.Any(r => r.Status == "resolved");
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

                    var automaticPricesAvailable = saved.AssetListing is not null
                        ? saved.AssetListing.HasEnabledSymbol
                        : importedHasAutomaticPrices ?? true;

                    result = saved.ToDto() with { AutomaticPricesAvailable = automaticPricesAvailable };
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

            try
            {
                await cacheInvalidator.InvalidateUser((int)userId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Transaction {TransactionId} was saved, but cache invalidation for user {UserId} failed", result.Id, userId);
            }

            return result;
        }
        catch (InvestmentTransactionCommandException)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Adding investment transaction cancelled for account {AccountId}.", request.AccountId);
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
                externalGate!.Release();
        }
    }

    private async Task ValidateTransactionCurrencyAsync(
        string? listingTradingCurrency,
        string normalizedRequestCurrency,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(listingTradingCurrency))
        {
            if (!string.Equals(listingTradingCurrency.Trim(), normalizedRequestCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvestmentTransactionCommandException(
                    "currency_mismatch",
                    $"Transaction currency must match the listing's trading currency ({listingTradingCurrency.Trim().ToUpperInvariant()}).");
            }

            return;
        }

        if (!await IsKnownCurrencyAsync(normalizedRequestCurrency, cancellationToken))
        {
            throw new InvestmentTransactionCommandException(
                "invalid_currency",
                "Select a predefined currency for the transaction.");
        }
    }

    private async Task<bool> IsKnownCurrencyAsync(string code, CancellationToken cancellationToken)
    {
        if (string.Equals(code, DefaultCurrency.PLN.ShortName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, DefaultCurrency.USD.ShortName, StringComparison.OrdinalIgnoreCase)
            || DefaultCurrency.CommonCurrencies.Any(c => string.Equals(c.ShortName, code, StringComparison.OrdinalIgnoreCase))
            || DefaultCurrency.MinorQuoteUnits.ContainsKey(code))
        {
            return true;
        }

        return await currencyRepository.GetByCode(code, cancellationToken) is not null;
    }

    private static string GetExternalGateKey(InstrumentDiscoveryResultDto instrument) =>
        $"{instrument.Ticker.Trim().ToUpperInvariant()}:{instrument.ExchangeMic ?? instrument.ExchangeCode}:{instrument.TradingCurrency}:{instrument.ListingFigi}:{instrument.Isin}";

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