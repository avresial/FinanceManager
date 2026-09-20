using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.TransactionRules.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Route("api/TransactionRules")]
[Tags("Transaction Automation Rules")]
public sealed class TransactionRuleController(ITransactionRuleService ruleService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<TransactionRuleDto>))]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        return Ok(await ruleService.GetRulesAsync(userId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionRuleDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var rule = await ruleService.GetRuleAsync(userId, id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(TransactionRuleDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRule command, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            var rule = await ruleService.CreateRuleAsync(userId, command, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionRuleDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransactionRule command, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            var rule = await ruleService.UpdateRuleAsync(userId, id, command, cancellationToken);
            return rule is null ? NotFound() : Ok(rule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id:guid}/enabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabled(Guid id, [FromBody] bool enabled, CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        return await ruleService.SetEnabledAsync(userId, id, enabled, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        return await ruleService.DeleteRuleAsync(userId, id, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("reorder")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<TransactionRuleDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder([FromBody] ReorderTransactionRules command, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            var rules = await ruleService.ReorderAsync(userId, command, cancellationToken);
            return rules is null ? BadRequest("Unable to reorder rules.") : Ok(rules);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("preview")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FinanceManager.Domain.TransactionRules.TransactionRuleEngineResult))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview([FromBody] TransactionRulePreviewFacts facts, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            return Ok(await ruleService.PreviewAsync(userId, facts, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("apply")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionRuleApplyResultDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Apply([FromBody] ApplyTransactionRules command, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            return Ok(await ruleService.ApplyRetroactivelyAsync(userId, command, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}