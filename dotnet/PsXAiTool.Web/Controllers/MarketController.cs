using Microsoft.AspNetCore.Mvc;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Web.Controllers;

[ApiController]
[Route("api/market")]
public class MarketController(IMarketService market) : ControllerBase
{
    [HttpGet("indices")]
    public async Task<IActionResult> GetIndices() => Ok(await market.GetIndicesAsync());

    [HttpGet("top-movers")]
    public async Task<IActionResult> GetTopMovers([FromQuery] int count = 5) =>
        Ok(await market.GetTopMoversAsync(count));

    [HttpGet("sectors")]
    public async Task<IActionResult> GetSectors() => Ok(await market.GetSectorsAsync());

    [HttpGet("stocks")]
    public async Task<IActionResult> GetStocks() => Ok(await market.GetStocksAsync());

    [HttpGet("stocks/{symbol}/prices")]
    public async Task<IActionResult> GetStockPrices(string symbol, [FromQuery] int days = 90) =>
        Ok(await market.GetStockPricesAsync(symbol, days));

    [HttpPost("admin/fetch-prices")]
    public async Task<IActionResult> TriggerFetch()
    {
        await market.FetchAndStorePricesAsync();
        return Ok(new { message = "Price fetch triggered." });
    }
}
