using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NewVisitorsController(INewVisitsRepository newVisitsRepository) : ControllerBase
{

    [AllowAnonymous]
    [HttpPut(Name = "AddNewVisitor")]
    public async Task<IActionResult> AddNewVisitor()
    {
        try
        {
            if (await newVisitsRepository.AddVisitAsync(DateTime.UtcNow))
                return Ok(new { message = "Visit recorded successfully" });

            return BadRequest(new { error = "Failed to record visit" });
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "An error occurred while recording the visit" });
        }
    }

    [Authorize]
    [HttpGet("GetNewVisitor/{dateTime:DateTime}")]
    public async Task<IActionResult> GetNewVisitor(DateTime dateTime)
    {
        try
        {
            return Ok(await newVisitsRepository.GetVisitAsync(dateTime));
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "An error occurred while retrieving visit data." });
        }
    }
}
