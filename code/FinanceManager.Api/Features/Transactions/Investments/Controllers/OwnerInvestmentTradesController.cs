using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.ValueObjects;
using FinanceManager.Domain.Labels.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Features.Transactions.Investments.Controllers;

/// <summary>
/// The caller's trades pooled across brokers. A trade belongs to its owner (<c>UserId</c>) and needs no account;
/// a trade of another user is reported as not found.
/// </summary>
[Authorize]
[ApiController]
[Route("api/transactions/investment")]
[Tags("Owner Investment Trades")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class OwnerInvestmentTradesController(
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentTransactionService transactionService,
    InvestmentTransactionPriceRecoveryService priceRecoveryService,
    IAssetListingRepository assetListingRepository,
    IFinancialLabelsRepository labelsRepository,
    ICacheInvalidator cacheInvalidator,
    IFinancialAlertService financialAlertService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<InvestmentTransactionHistoryPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistoryPage(
        [FromQuery, Range(1, 100)] int pageSize = 50,
        [FromQuery] string? cursor = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] long? assetListingId = null,
        [FromQuery] InvestmentTransactionType? type = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (type is not null && !Enum.IsDefined(type.Value)) return BadRequest("Unknown trade type.");

        DateOnly? cursorDate = null;
        long? cursorId = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!InvestmentHistoryCursor.TryCreate(cursor, out var parsed)) return BadRequest("Invalid cursor format.");
            cursorDate = parsed.TradeDate;
            cursorId = parsed.Id;
        }

        var (items, hasMore) = await transactionRepository.GetHistoryPageByUser(
            ownerId, pageSize, cursorDate, cursorId, startDate, endDate, assetListingId, type, search, cancellationToken);
        await priceRecoveryService.RecoverMissingPricesAsync(items, ownerId, cancellationToken);

        var nextCursor = hasMore && items.Count > 0
            ? InvestmentHistoryCursor.Create(items[^1].TradeDate, items[^1].EntryId).ToString()
            : null;
        return Ok(new InvestmentTransactionHistoryPageDto([.. items.Select(x => x.ToDto())], hasMore, nextCursor));
    }

    /// <summary>Signed units per listing across all of the caller's trades, as of a date (default today, UTC). Unpaged: a user holds few listings.</summary>
    [HttpGet("holdings")]
    [ProducesResponseType<IReadOnlyList<InvestmentHoldingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHoldings([FromQuery] DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        var holdings = await transactionRepository.GetHoldingsByUserAsOf(ownerId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        return Ok(holdings.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<InvestmentTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        var trade = await transactionRepository.Get(id, cancellationToken);
        if (trade is null || trade.UserId != ownerId) return NotFound();

        await priceRecoveryService.RecoverMissingPricesAsync([trade], ownerId, cancellationToken);
        return Ok(trade.ToDto());
    }

    [HttpPost]
    [ProducesResponseType<InvestmentTransactionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(CreateInvestmentTradeRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (!Enum.IsDefined(request.Type)) return BadRequest("Unknown trade type.");
        if (!await AreLabelsValid(request.LabelIds, cancellationToken)) return BadRequest("Unknown label ID.");

        try
        {
            var created = await transactionService.AddOwnerAsync(request, ownerId, cancellationToken);
            await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvestmentTransactionCommandException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Could not save the transaction.",
                detail: ex.Message,
                type: $"https://financemanager.dev/problems/{ex.Code}",
                extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
        }
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(long id, UpdateInvestmentTradeRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (request.AssetListingId <= 0 || request.Quantity <= 0 || request.UnitPrice < 0
            || string.IsNullOrWhiteSpace(request.Currency) || request.TradeDate == default || !Enum.IsDefined(request.Type))
            return BadRequest("Invalid input parameters.");

        var existing = await transactionRepository.Get(id, cancellationToken);
        if (existing is null || existing.UserId != ownerId) return NotFound();

        if (await assetListingRepository.Get(request.AssetListingId, cancellationToken) is null) return BadRequest("Unknown listing.");
        if (!await AreLabelsValid(request.LabelIds, cancellationToken)) return BadRequest("Unknown label ID.");

        var trade = request.ToEntity(id);
        // Omitted LabelIds leaves labels unchanged; an empty list clears them.
        if (request.LabelIds is null)
            trade.Labels = existing.Labels;
        if (!await transactionRepository.Update(trade, cancellationToken)) return NotFound();

        await cacheInvalidator.InvalidateUser(ownerId);
        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        var existing = await transactionRepository.Get(id, cancellationToken);
        if (existing is null || existing.UserId != ownerId) return NotFound();
        if (!await transactionRepository.Delete(id, cancellationToken)) return NotFound();

        await cacheInvalidator.InvalidateUser(ownerId);
        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return NoContent();
    }

    private async Task<bool> AreLabelsValid(IReadOnlyCollection<int>? ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0) return true;
        var available = new HashSet<int>();
        await foreach (var label in labelsRepository.GetLabels(cancellationToken)) available.Add(label.Id);
        return ids.All(available.Contains);
    }
}