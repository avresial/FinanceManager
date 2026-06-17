using FinanceManager.Api.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class ExpenseDistributionController(IExpenseDistributionService expenseDistributionService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("GetExpenseDistribution/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetExpenseDistribution(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(await expenseDistributionService.GetExpenseDistribution(userId, currency, start, end));
    }
}