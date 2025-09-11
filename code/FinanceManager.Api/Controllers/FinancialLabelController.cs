using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;
[Route("api/[controller]")]
[ApiController]
public class FinancialLabelController(IFinancialLabelsRepository financialLabelsRepository) : ControllerBase
{

    [HttpGet("get-by-id")]
    public async Task<IActionResult> GetById([FromQuery] int id)
    {
        try
        {
            return Ok(await financialLabelsRepository.GetLabelsById(id));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("get-by-account-id")]
    public async Task<IActionResult> GetByAccountId([FromQuery] int accountId)
    {
        try
        {
            return Ok(await financialLabelsRepository.GetLabelsByAccountId(accountId).ToListAsync());
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
            var result = await financialLabelsRepository.GetLabels().Skip(index).Take(count).ToListAsync();


            return Ok(result);
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
            return Ok(await financialLabelsRepository.GetCount());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add(AddFinancialLabel addFinancialLabel)
    {
        return Ok(await financialLabelsRepository.Add(addFinancialLabel.Name));
    }

    [HttpPost("update-name")]
    public async Task<IActionResult> UpdateName([FromQuery] int id, string name)
    {
        return Ok(await financialLabelsRepository.UpdateName(id, name));
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] int id)
    {
        return Ok(await financialLabelsRepository.Delete(id));
    }
}
