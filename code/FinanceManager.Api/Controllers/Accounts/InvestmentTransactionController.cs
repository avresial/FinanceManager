using FinanceManager.Api.Helpers;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Transactions")]
public class InvestmentTransactionController(
    IAccountRepository<StockAccount> accountRepository,
    IInvestmentTransactionRepository transactionRepository,
    ICacheInvalidator dashboardCacheInvalidator) : ControllerBase
{
    [HttpGet("GetByAccount/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InvestmentTransactionDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByAccount(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var transactions = await transactionRepository.GetByAccount(accountId, cancellationToken);
        return Ok(transactions.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("Get/{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentTransactionDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.Get(id, cancellationToken);
        if (transaction is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, (int)transaction.UserId)) return Forbid();

        return Ok(transaction.ToDto());
    }

    [HttpPost("Add")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentTransactionDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add(AddInvestmentTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValid(request.AssetListingId, request.Quantity, request.UnitPrice, request.Currency, request.TradeDate))
            return BadRequest("Invalid input parameters.");

        var account = await accountRepository.Get(request.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var result = await transactionRepository.Add(request.ToEntity(account.UserId), cancellationToken);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result.ToDto());
    }

    [HttpPut("Update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(UpdateInvestmentTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValid(request.AssetListingId, request.Quantity, request.UnitPrice, request.Currency, request.TradeDate))
            return BadRequest("Invalid input parameters.");

        var account = await accountRepository.Get(request.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var existing = await transactionRepository.Get(request.Id, cancellationToken);
        if (existing is null || existing.AccountId != request.AccountId) return NotFound();

        var result = await transactionRepository.Update(request.ToEntity(), cancellationToken);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }

    [HttpDelete("Delete/{accountId:int}/{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int accountId, long id, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var existing = await transactionRepository.Get(id, cancellationToken);
        if (existing is null || existing.AccountId != accountId) return NotFound();

        var result = await transactionRepository.Delete(id, cancellationToken);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }

    private static bool IsValid(long assetListingId, decimal quantity, decimal unitPrice, string? currency, DateOnly tradeDate) =>
        assetListingId > 0 && quantity > 0 && unitPrice >= 0 && !string.IsNullOrWhiteSpace(currency) && tradeDate != default;
}