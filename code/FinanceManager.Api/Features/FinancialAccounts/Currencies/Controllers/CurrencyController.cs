using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
[Tags("Currencies")]
public class CurrencyController(ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("GetAll")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<Currency>))]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(await currencyRepository.GetCurrencies(cancellationToken).OrderBy(x => x.ShortName).ToListAsync(cancellationToken));
}