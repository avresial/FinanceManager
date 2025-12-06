using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Visitor Tracking")]
public class NewVisitorsController(INewVisitsRepository newVisitsRepository) : ControllerBase
{

    [AllowAnonymous]
    [HttpPut(Name = "AddNewVisitor")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddNewVisitor(CancellationToken cancellationToken = default)
    {
        if (await newVisitsRepository.AddVisitAsync(DateTime.UtcNow))
            return Ok(new { message = "Visit recorded successfully" });

        return BadRequest(new { error = "Failed to record visit" });
    }

    [Authorize]
    [HttpGet("GetNewVisitor/{dateTime:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
    public async Task<IActionResult> GetNewVisitor(DateTime dateTime, CancellationToken cancellationToken = default) => Ok(await newVisitsRepository.GetVisitAsync(dateTime));
}