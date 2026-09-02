using Microsoft.AspNetCore.Mvc;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Web.Controllers;

[ApiController]
[Route("api/portfolio")]
public class PortfolioController(IPortfolioService portfolio) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHoldings() => Ok(await portfolio.GetHoldingsAsync());

    [HttpPost]
    public async Task<IActionResult> AddHolding([FromBody] AddPortfolioRequest request)
    {
        try
        {
            var item = await portfolio.AddHoldingAsync(request);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{symbol}")]
    public async Task<IActionResult> UpdateHolding(string symbol, [FromBody] UpdatePortfolioRequest request)
    {
        var item = await portfolio.UpdateHoldingAsync(symbol, request);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> DeleteHolding(string symbol)
    {
        var deleted = await portfolio.DeleteHoldingAsync(symbol);
        return deleted ? NoContent() : NotFound();
    }
}
