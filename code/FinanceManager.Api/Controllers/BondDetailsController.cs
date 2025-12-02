using FinanceManager.Application.Commands.Bonds;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[ApiController]
public class BondDetailsController(IBondDetailsRepository bondDetailsRepository, IBondService bondService) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var bond = await bondDetailsRepository.GetByIdAsync(id, cancellationToken);
        if (bond is null) return NotFound();

        return Ok(bond);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
     Ok(await bondDetailsRepository.GetAllAsync(cancellationToken).ToListAsync(cancellationToken: cancellationToken));

    [HttpGet("by-issuer/{issuer}")]
    public async Task<IActionResult> GetByIssuer(string issuer, CancellationToken cancellationToken) =>
     Ok(await bondDetailsRepository.GetByIssuerAsync(issuer, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] BondDetails bond, CancellationToken cancellationToken)
    {
        var id = await bondDetailsRepository.AddAsync(bond, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, bond);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateBondDetails updateBondDetails, CancellationToken cancellationToken)
    {
        var bond = await bondDetailsRepository.GetByIdAsync(updateBondDetails.Id, cancellationToken);
        if (bond is null)
            return NotFound();

        bond.Name = updateBondDetails.NameToUpdate;
        bond.Issuer = updateBondDetails.IssuerToUpdate;

        if (!await bondDetailsRepository.UpdateAsync(bond, cancellationToken))
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!await bondDetailsRepository.DeleteAsync(id, cancellationToken))
            return NotFound();

        return NoContent();
    }

    [HttpPost("{bondDetailsId:int}/calculation-methods")]
    public async Task<IActionResult> AddCalculationMethod(int bondDetailsId, [FromBody] BondCalculationMethod calculationMethod, CancellationToken cancellationToken)
    {
        if (!await bondService.AddCalculationMethodAsync(bondDetailsId, calculationMethod, cancellationToken))
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{bondDetailsId:int}/calculation-methods/{calculationMethodId:int}")]
    public async Task<IActionResult> RemoveCalculationMethod(int bondDetailsId, int calculationMethodId, CancellationToken cancellationToken)
    {
        if (!await bondService.RemoveCalculationMethodAsync(bondDetailsId, calculationMethodId, cancellationToken))
            return NotFound();

        return NoContent();
    }
}