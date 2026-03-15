using FinanceManager.Api.Helpers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("CSV Header Mappings")]
public class CsvHeaderMappingController(ICsvHeaderMappingService mappingService) : ControllerBase
{
    /// <summary>
    /// Get suggested header mappings based on previously saved mappings.
    /// </summary>
    [HttpPost("SuggestMappings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<HeaderMappingResultDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SuggestMappings([FromBody] IEnumerable<string> headers)
    {
        if (headers is null || !headers.Any())
            return BadRequest("Headers list is required and cannot be empty.");

        var suggestions = await mappingService.GetSuggestedMappingsAsync(headers);

        return Ok(suggestions);
    }

    /// <summary>
    /// Save or update header mappings for the user.
    /// </summary>
    [HttpPost("SaveMappings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveMappings([FromBody] SaveMappingRequestDto mappingRequest)
    {
        if (mappingRequest is null || !mappingRequest.Mappings.Any())
            return BadRequest("Mapping request is required and cannot be empty.");

        await mappingService.SaveMappingsAsync(mappingRequest);

        return Ok();
    }
}
