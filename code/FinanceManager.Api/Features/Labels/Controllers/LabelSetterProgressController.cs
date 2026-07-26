using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Labels.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Labels.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
[Tags("Label setter progress")]
public class LabelSetterProgressController(ILabelSetterProgressTracker progressTracker) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LabelSetterProgressSnapshot))]
    public IActionResult Get() => Ok(progressTracker.GetSnapshot());
}