using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
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
public class AssetsController(IAssetsService assetsService, IInvestmentPaycheckEstimatorService investmentPaycheckEstimatorService, ICurrencyRepository currencyRepository) : ControllerBase
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

    [HttpGet("GetInvestmentPaycheckEstimate/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentPaycheckEstimate))]
    public async Task<IActionResult> GetInvestmentPaycheckEstimate(int userId, int currencyId, DateTime asOfDate, [FromQuery] decimal withdrawalRate = 0.05m, [FromQuery] int salaryMonths = 3, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await investmentPaycheckEstimatorService.GetEstimate(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate, withdrawalRate, salaryMonths)) : Forbid();

    [HttpGet("GetUnrealizedGainLossPerAccount/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UnrealizedGainLossAccountResult>))]
    public async Task<IActionResult> GetUnrealizedGainLossPerAccount(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetUnrealizedGainLossPerAccount(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate)) : Forbid();

    [HttpGet("GetUnrealizedGainLossPerInstrument/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UnrealizedGainLossInstrumentResult>))]
    public async Task<IActionResult> GetUnrealizedGainLossPerInstrument(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await assetsService.GetUnrealizedGainLossPerInstrument(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate)) : Forbid();

}