using FinanceManager.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NewVisitorsController(NewVisitsRepository newVisitsRepository) : ControllerBase
{
    private readonly NewVisitsRepository _newVisitsRepository = newVisitsRepository;

    [AllowAnonymous]
    [HttpPut(Name = "AddNewVisitor")]
    public async Task<IActionResult> AddNewVisitor()
    {
        if (await _newVisitsRepository.AddVisitAsync(DateTime.UtcNow)) return Ok();

        return BadRequest();
    }

    //[Authorize]
    [AllowAnonymous]
    [HttpGet("GetNewVisitor/{dateTime:DateTime}")]
    public async Task<IActionResult> GetNewVisitor(DateTime dateTime)
    {
        return Ok(await _newVisitsRepository.GetVisitAsync(dateTime));
    }

}
