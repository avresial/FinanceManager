using FinanceManager.Application.Services;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

//[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DuplicateEntryResolverController(DuplicateEntryResolverService duplicateEntryResolverService, IDuplicateEntryRepository duplicateEntryRepository) : ControllerBase
{

    [HttpGet("GetDuplicatesCount")]
    public async Task<IActionResult> GetDuplicatesCount([FromQuery] int accountId)
    {
        try
        {
            return Ok(await duplicateEntryRepository.GetDuplicatesCount(accountId));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("GetDuplicates")]
    public async Task<IActionResult> GetDuplicates([FromQuery] int accountId, [FromQuery] int index, [FromQuery] int count)
    {
        try
        {
            return Ok(await duplicateEntryRepository.GetDuplicates(accountId, index, count));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    [HttpPost("Scan")]
    public async Task<IActionResult> Scan([FromQuery] int accountId)
    {
        try
        {
            await duplicateEntryResolverService.Scan(accountId);
            return Ok(new { message = "Scan completed." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("Resolve")]
    public async Task<IActionResult> Resolve([FromQuery] int accountId, [FromQuery] int duplicateId, [FromQuery] int entryIdToBeRemained)
    {
        try
        {
            if (await duplicateEntryResolverService.Resolve(accountId, duplicateId, entryIdToBeRemained))
                return Ok(new { message = "Duplicate resolved." });
            else
                return NotFound(new { message = "Duplicate entry not found or already resolved." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("RemoveDuplicate")]
    public async Task<IActionResult> RemoveDuplicate([FromQuery] int duplicateId)
    {
        try
        {
            await duplicateEntryRepository.RemoveDuplicate(duplicateId);
            return Ok(new { message = "Duplicate removed." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


}
