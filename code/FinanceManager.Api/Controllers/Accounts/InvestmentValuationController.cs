using FinanceManager.Api.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Valuation")]
public class InvestmentValuationController(
    IAccountRepository<StockAccount> accountRepository,
    IInvestmentValuationService valuationService,
    ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("Holdings/{accountId:int}/{date:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyDictionary<long, decimal>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHoldings(int accountId, DateTime date, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var holdings = await valuationService.GetHoldingsAsOfAsync(accountId, DateOnly.FromDateTime(date), cancellationToken);
        return Ok(holdings);
    }

    [HttpGet("Value/{accountId:int}/{currencyId:int}/{date:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(decimal))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetValue(int accountId, int currencyId, DateTime date, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        var value = await valuationService.GetAccountValueAsync(accountId, currency, date, cancellationToken);
        return Ok(value);
    }

    [HttpGet("ValueSeries/{accountId:int}/{currencyId:int}/{startDate:DateTime}/{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyDictionary<DateTime, decimal>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetValueSeries(int accountId, int currencyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (endDate < startDate) return BadRequest("End date must be on or after start date.");

        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        var series = await valuationService.GetAccountValueSeriesAsync(accountId, currency, startDate, endDate, cancellationToken);
        return Ok(series);
    }
}