using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FinancialLabelController(IFinancialLabelsRepository financialLabelsRepository) : ControllerBase
{

    [HttpGet("get-by-id")]
    public async Task<IActionResult> GetById([FromQuery] int id) =>
        Ok(await financialLabelsRepository.GetLabelsById(id));

    [HttpGet("get-by-account-id")]
    public async Task<IActionResult> GetByAccountId([FromQuery] int accountId) =>
        Ok(await financialLabelsRepository.GetLabelsByAccountId(accountId).ToListAsync());

    [HttpGet("get-by-index-and-count")]
    public async Task<IActionResult> GetByIndexAndCount([FromQuery] int index, [FromQuery] int count) =>
        Ok(await financialLabelsRepository.GetLabels().Skip(index).Take(count).ToListAsync());

    [HttpGet("get-count")]
    public async Task<IActionResult> GetCount() => Ok(await financialLabelsRepository.GetCount());

    [HttpPost("add")]
    public async Task<IActionResult> Add(AddFinancialLabel addFinancialLabel) => Ok(await financialLabelsRepository.Add(addFinancialLabel.Name));

    [HttpPost("update-name")]
    public async Task<IActionResult> UpdateName([FromQuery] int id, string name) => Ok(await financialLabelsRepository.UpdateName(id, name));

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] int id) => Ok(await financialLabelsRepository.Delete(id));
}