using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Alerts.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Financial Alerts")]
public sealed class FinancialAlertsController(IFinancialAlertService alertService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<FinancialAlertDto>))]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var alerts = await alertService.GetAlertsAsync(userId, cancellationToken);
        return Ok(alerts.Select(FinancialAlertDto.FromEntity).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FinancialAlertDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var alert = await alertService.GetAlertByIdAsync(userId, id, cancellationToken);
        return alert is null ? NotFound() : Ok(FinancialAlertDto.FromEntity(alert));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FinancialAlertDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFinancialAlert command,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
            return BadRequest(validationError);

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var alert = await alertService.CreateAlertAsync(userId, command, cancellationToken);
        var dto = FinancialAlertDto.FromEntity(alert);
        return CreatedAtAction(nameof(Get), new { id = alert.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FinancialAlertDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateFinancialAlert command,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
            return BadRequest(validationError);

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var alert = await alertService.UpdateAlertAsync(userId, id, command, cancellationToken);
        return alert is null ? NotFound() : Ok(FinancialAlertDto.FromEntity(alert));
    }

    [HttpPatch("{id:guid}/enabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabled(
        Guid id,
        [FromBody] bool enabled,
        CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        return await alertService.SetEnabledAsync(userId, id, enabled, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        return await alertService.DeleteAlertAsync(userId, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("evaluate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<AlertEvaluationOutcome>))]
    public async Task<IActionResult> Evaluate(CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        return Ok(await alertService.EvaluateAlertsAsync(userId, cancellationToken));
    }

    [HttpPost("{id:guid}/evaluate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AlertEvaluationOutcome))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var outcome = await alertService.EvaluateAlertAsync(userId, id, cancellationToken);
        return outcome is null ? NotFound() : Ok(outcome);
    }

    private static string? Validate(CreateFinancialAlert command)
    {
        if (command is null)
            return "Alert payload is required.";

        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Trim().Length > 200)
            return "Title is required and must be at most 200 characters.";

        if (command.MerchantName?.Length > 200 || command.LabelName?.Length > 200)
            return "Scope names must be at most 200 characters.";

        if (command.CooldownPeriod is { } cooldown && cooldown < TimeSpan.Zero)
            return "Cooldown period cannot be negative.";

        return null;
    }

    private static string? Validate(UpdateFinancialAlert command)
    {
        if (command is null)
            return "Alert payload is required.";

        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Trim().Length > 200)
            return "Title is required and must be at most 200 characters.";

        if (command.MerchantName?.Length > 200 || command.LabelName?.Length > 200)
            return "Scope names must be at most 200 characters.";

        if (command.CooldownPeriod is { } cooldown && cooldown < TimeSpan.Zero)
            return "Cooldown period cannot be negative.";

        return null;
    }
}