using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MoneyFlowController(IMoneyFlowService moneyFlowService) : ControllerBase
    {
        [HttpGet("GetNetWorth/{userId:int}/{currency}/{date:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, string currency, DateTime date) =>
            Ok(await moneyFlowService.GetNetWorth(userId, currency, date));

        [HttpGet("GetNetWorth/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, string currency, DateTime start, DateTime end) =>
            Ok(await moneyFlowService.GetNetWorth(userId, currency, start, end));

        [HttpGet("GetIncome/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{step:long?}")]
        public async Task<IActionResult> GetIncome(int userId, string currency, DateTime start, DateTime end, long? step = null) =>
            Ok(await moneyFlowService.GetIncome(userId, currency, start, end, step is null ? null : new(step.Value)));

        [HttpGet("GetSpending/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{step:long?}")]
        public async Task<IActionResult> GetSpending(int userId, string currency, DateTime start, DateTime end, long? step = null) =>
            Ok(await moneyFlowService.GetSpending(userId, currency, start, end, step is null ? null : new(step.Value)));

        [HttpGet("GetLabelsValue")]
        public async Task<IActionResult> GetLabelsValue([FromQuery] int userId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long? step = null) =>
            Ok(await moneyFlowService.GetLabelsValue(userId, start, end, step is null ? null : new(step.Value)));
    }
}
