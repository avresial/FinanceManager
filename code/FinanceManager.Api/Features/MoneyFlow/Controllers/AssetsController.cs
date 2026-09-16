using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.MoneyFlow.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
[Tags("Financial Analysis")]
public class AssetsController(
    IAssetsService assetsService,
    IInvestmentPaycheckEstimatorService investmentPaycheckEstimatorService,
    IFeeDragService feeDragService,
    ICurrencyRepository currencyRepository,
    IInvestmentAppreciationService investmentAppreciationService,
    IAccountRepository<InvestmentAccount> accountRepository,
    IMoneyWeightedReturnService moneyWeightedReturnService) : ControllerBase
{
    [HttpGet("IsAnyAccountWithAssets/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    public async Task<IActionResult> IsAnyAccountWithAssets(int userId, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.IsAnyAccountWithAssets(userId)) : Forbid();

    [HttpGet("GetEndAssetsPerAccount/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetEndAssetsPerAccount(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetEndAssetsPerAccount(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate).ToListAsync(cancellationToken)) : Forbid();

    [HttpGet("GetEndAssetsPerType/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetEndAssetsPerType(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetEndAssetsPerType(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate).ToListAsync(cancellationToken)) : Forbid();

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetAssetsTimeSeries(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), start, end)) : Forbid();

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{investmentType}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, int currencyId, DateTime start, DateTime end, InvestmentType investmentType, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetAssetsTimeSeries(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), start, end, investmentType)) : Forbid();

    [HttpGet("GetMoneyWeightedReturn/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MoneyWeightedReturnResult))]
    public async Task<IActionResult> GetMoneyWeightedReturn(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId)
            ? Ok(await moneyWeightedReturnService.GetAsync(
                userId,
                await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken),
                start,
                end,
                cancellationToken))
            : Forbid();

    [HttpGet("GetInvestmentPaycheckEstimate/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentPaycheckEstimate))]
    public async Task<IActionResult> GetInvestmentPaycheckEstimate(int userId, int currencyId, DateTime asOfDate, [FromQuery] decimal withdrawalRate = 0.05m, [FromQuery] int salaryMonths = 3, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await investmentPaycheckEstimatorService.GetEstimate(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate, withdrawalRate, salaryMonths)) : Forbid();

    [HttpGet("GetFeeDragAnalysis/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeeDragAnalysisResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFeeDragAnalysis(
        int userId,
        int currencyId,
        DateTime asOfDate,
        [FromQuery] decimal assumedAnnualReturnRate = 0.07m,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        if (assumedAnnualReturnRate is < IFeeDragService.MinimumReturnRate or > IFeeDragService.MaximumReturnRate)
            return BadRequest("The assumed annual return rate must be between -10% and 10%.");

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null)
            return NotFound("Currency not found.");

        return Ok(await feeDragService.GetAnalysisAsync(
            userId,
            currency,
            asOfDate,
            assumedAnnualReturnRate,
            cancellationToken));
    }

    [HttpGet("GetUnrealizedGainLossPerAccount/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UnrealizedGainLossAccountResult>))]
    public async Task<IActionResult> GetUnrealizedGainLossPerAccount(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetUnrealizedGainLossPerAccount(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate)) : Forbid();

    [HttpGet("GetUnrealizedGainLossForAccount/{userId:int}/{accountId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UnrealizedGainLossAccountResult))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUnrealizedGainLossForAccount(
        int userId,
        int accountId,
        int currencyId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        var account = await accountRepository.Get(accountId, cancellationToken);
        if (account is null)
            return NotFound();

        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId) || account.UserId != userId)
            return NotFound();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null)
            return NotFound("Currency not found.");

        var result = await investmentAppreciationService.GetForAccountAsync(
            userId,
            accountId,
            currency,
            asOfDate,
            cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("GetUnrealizedGainLossPerInstrument/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UnrealizedGainLossInstrumentResult>))]
    public async Task<IActionResult> GetUnrealizedGainLossPerInstrument(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetUnrealizedGainLossPerInstrument(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate)) : Forbid();
}