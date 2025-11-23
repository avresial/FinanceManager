using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize (Roles = "Admin")]
[ApiController]
public class BondDetailsController(IBondDetailsRepository bondDetailsRepository) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var bond = await bondDetailsRepository.GetByIdAsync(id, cancellationToken);
        if (bond == null) return NotFound();
        
        return Ok(bond);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)=>
     Ok(await bondDetailsRepository.GetAllAsync(cancellationToken).ToListAsync(cancellationToken: cancellationToken));

    [HttpGet("by-issuer/{issuer}")]
    public async Task<IActionResult> GetByIssuer(string issuer, CancellationToken cancellationToken)=>
     Ok(await bondDetailsRepository.GetByIssuerAsync(issuer, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] BondDetails bond, CancellationToken cancellationToken)
    {
        var id = await bondDetailsRepository.AddAsync(bond, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, bond);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BondDetails bond, CancellationToken cancellationToken)
    {
        if (id != bond.Id)
            return BadRequest("ID mismatch");

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
}