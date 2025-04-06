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
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetEndAssetsPerType/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetEndAssetsPerType(int userId, DateTime start, DateTime end)
        {
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetAssetsTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetAssetsTimeSeries(int userId, DateTime start, DateTime end)
        {
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetAssetsTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}/{investmentType}")]
        public async Task<IActionResult> GetAssetsTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
        {
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetNetWorth/{userId:int}/{start:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, DateTime date)
        {
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetNetWorth/{userId:int}/{start:DateTime}/{end:DateTime}")]
        public async Task<IActionResult> GetNetWorth(int userId, DateTime start, DateTime end)
        {
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetIncome/{userId:int}/{start:DateTime}/{end:DateTime}/{step}")]
        public async Task<IActionResult> GetIncome(int userId, DateTime start, DateTime end, TimeSpan? step = null)
        {
            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetSpending/{userId:int}/{start:DateTime}/{end:DateTime}/{step}")]
        public async Task<IActionResult> GetSpending(int userId, DateTime start, DateTime end, TimeSpan? step = null)
        {
            return await Task.FromResult(NoContent());
        }
    }
}
