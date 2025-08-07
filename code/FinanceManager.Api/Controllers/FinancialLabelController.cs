using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;
[Route("api/[controller]")]
[ApiController]
public class FinancialLabelController : ControllerBase
{
    [HttpGet("get-by-id")]
    public async Task<IActionResult> GetById([FromQuery] int accountId)
    {
        try
        {
            return Ok(null);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("get-by-index-and-count")]
    public async Task<IActionResult> GetByIndexAndCount([FromQuery] int index, [FromQuery] int count)
    {
        try
        {
            return Ok(new List<FinancialLabel>());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    [HttpGet("get-count")]
    public async Task<IActionResult> GetCount()
    {
        try
        {
            return Ok(0);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
