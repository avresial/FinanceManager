using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FinancialLabelController(IFinancialLabelsRepository financialLabelsRepository) : ControllerBase
{

    [HttpGet("get-by-id")]
    public async Task<IActionResult> GetById([FromQuery] int id, CancellationToken cancellationToken = default) =>
        Ok(await financialLabelsRepository.GetLabelsById(id, cancellationToken));

    [HttpGet("get-by-account-id")]
    public async Task<IActionResult> GetByAccountId([FromQuery] int accountId, CancellationToken cancellationToken = default) =>
        Ok(await financialLabelsRepository.GetLabelsByAccountId(accountId, cancellationToken).ToListAsync(cancellationToken));

    [HttpGet("get-by-index-and-count")]
    public async Task<IActionResult> GetByIndexAndCount([FromQuery] int index, [FromQuery] int count, CancellationToken cancellationToken = default) =>
        Ok(await financialLabelsRepository.GetLabels(cancellationToken).Skip(index).Take(count).ToListAsync(cancellationToken));

    [HttpGet("get-count")]
    public async Task<IActionResult> GetCount(CancellationToken cancellationToken = default) => Ok(await financialLabelsRepository.GetCount(cancellationToken));

    [HttpPost("add")]
    public async Task<IActionResult> Add(AddFinancialLabel addFinancialLabel, CancellationToken cancellationToken = default) =>
    Ok(await financialLabelsRepository.Add(addFinancialLabel.Name, cancellationToken));

    [HttpPost("update-name")]
    public async Task<IActionResult> UpdateName([FromQuery] int id, string name, CancellationToken cancellationToken = default) =>
    Ok(await financialLabelsRepository.UpdateName(id, name, cancellationToken));

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] int id, CancellationToken cancellationToken = default) =>
    Ok(await financialLabelsRepository.Delete(id, cancellationToken));
}