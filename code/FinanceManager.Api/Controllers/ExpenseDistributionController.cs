using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var authenticatedUserId))
        {
            return Forbid();
        }

        if (authenticatedUserId != userId)
        {
            return Forbid();
        }

        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(await expenseDistributionService.GetExpenseDistribution(userId, currency, start, end));
    }
}
