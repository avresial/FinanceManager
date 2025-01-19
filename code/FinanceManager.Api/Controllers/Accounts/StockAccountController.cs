using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts
{
    [Route("api/[controller]")]
    [ApiController]
    public class StockAccountController : ControllerBase
    {
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add()
        {
            var idValue = User?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            return NotFound();
            // if (result is not null) return Ok(result);
            return BadRequest();
        }

        [HttpPut]
        [Route("Update")]
        public async Task<IActionResult> Update()
        {
            var idValue = User?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            return NotFound();
            // if (result is not null) return Ok(result);
            return BadRequest();
        }


        [HttpDelete]
        [Route("Delete")]
        public async Task<IActionResult> Delete()
        {
            var idValue = User?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            return NotFound();
            // if (result is not null) return Ok(result);
            return BadRequest();
        }
    }
}
