using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
[Tags("Financial Analysis")]
public class DiversificationController(IDiversificationService diversificationService) : ControllerBase
{
    [HttpGet("{userId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DiversificationScore))]
    public async Task<IActionResult> GetDiversificationScore(int userId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        Ok(await diversificationService.GetDiversificationScore(userId, asOfDate));
}
