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

        [HttpGet("GetEndAssetsPerAcount/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetEndAssetsPerAcount(int userId, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetEndAssetsPerAcount(userId, start, end));
        }

        [HttpGet("GetEndAssetsPerType/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetEndAssetsPerType(int userId, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetEndAssetsPerType(userId, start, end));
        }

        [HttpGet("IsAnyAccountWithAssets/{userId:int}")]
        public async Task<IActionResult> IsAnyAccountWithAssets(int userId)
        {
            return Ok(await _moneyFlowService.IsAnyAccountWithAssets(userId));
        }

        [HttpGet("IsAnyAccountWithLiabilities/{userId:int}")]
        public async Task<IActionResult> IsAnyAccountWithLiabilities(int userId)
        {
            return Ok(await _moneyFlowService.IsAnyAccountWithLiabilities(userId));
        }

        [HttpGet("GetAssetsTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetAssetsTimeSeries(int userId, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetAssetsTimeSeries(userId, start, end));
        }

        [HttpGet("GetAssetsTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}/{investmentType}")]
        public async Task<IActionResult> GetAssetsTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
        {
            return Ok(await _moneyFlowService.GetAssetsTimeSeries(userId, start, end, investmentType));
        }

        [HttpGet("GetNetWorth/{userId:int}/{date:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, DateTime date)
        {
            return Ok(await _moneyFlowService.GetNetWorth(userId, date));
        }

        [HttpGet("GetNetWorth/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, DateTime start, DateTime end)
        {
            return Ok(await _moneyFlowService.GetNetWorth(userId, start, end));
        }

        [HttpGet("GetIncome/{userId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
        public async Task<IActionResult> GetIncome(int userId, DateTime start, DateTime end, long? step = null)
        {
            return Ok(await _moneyFlowService.GetIncome(userId, start, end, step is null ? null : new(step.Value)));
        }

        [HttpGet("GetSpending/{userId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
        public async Task<IActionResult> GetSpending(int userId, DateTime start, DateTime end, long? step = null)
        {
            return Ok(await _moneyFlowService.GetSpending(userId, start, end, step is null ? null : new(step.Value)));
        }
    }
}
