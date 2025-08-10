using FinanceManager.Domain.Enums;
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
        private readonly IMoneyFlowService _moneyFlowService = moneyFlowService;

        [HttpGet("IsAnyAccountWithAssets/{userId:int}")]
        public async Task<IActionResult> IsAnyAccountWithAssets(int userId)
        {
            return Ok(await _moneyFlowService.IsAnyAccountWithAssets(userId));
        }

        [HttpGet("GetEndAssetsPerAccount/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetEndAssetsPerAccount(int userId, string currency, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetEndAssetsPerAccount(userId, currency, start, end));
        }

        [HttpGet("GetEndAssetsPerType/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetEndAssetsPerType(int userId, string currency, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetEndAssetsPerType(userId, currency, start, end));
        }

        [HttpGet("GetAssetsTimeSeries/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetAssetsTimeSeries(userId, currency, start, end));
        }

        [HttpGet("GetAssetsTimeSeries/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{investmentType}")]
        public async Task<IActionResult> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end, InvestmentType investmentType)
        {
            return Ok(await _moneyFlowService.GetAssetsTimeSeries(userId, currency, start, end, investmentType));
        }

        [HttpGet("GetNetWorth/{userId:int}/{currency}/{date:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, string currency, DateTime date)
        {
            return Ok(await _moneyFlowService.GetNetWorth(userId, currency, date));
        }

        [HttpGet("GetNetWorth/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, string currency, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetNetWorth(userId, currency, start, end));
        }

        [HttpGet("GetIncome/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{step:long?}")]
        public async Task<IActionResult> GetIncome(int userId, string currency, DateTime start, DateTime end, long? step = null)
        {
            return Ok(await _moneyFlowService.GetIncome(userId, currency, start, end, step is null ? null : new(step.Value)));
        }

        [HttpGet("GetSpending/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{step:long?}")]
        public async Task<IActionResult> GetSpending(int userId, string currency, DateTime start, DateTime end, long? step = null)
        {
            return Ok(await _moneyFlowService.GetSpending(userId, currency, start, end, step is null ? null : new(step.Value)));
        }

        [HttpGet("GetLabelsValue")]
        public async Task<IActionResult> GetLabelsValue([FromQuery] int userId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long? step = null)
        {
            return Ok(await _moneyFlowService.GetLabelsValue(userId, start, end, step is null ? null : new(step.Value)));
        }
    }
}
