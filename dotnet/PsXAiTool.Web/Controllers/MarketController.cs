using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Core.Settings;
using PsXAiTool.Infrastructure.Scrapers;

namespace PsXAiTool.Web.Controllers;

[ApiController]
[Route("api/market")]
public class MarketController(
    IMarketService market,
    TwelveDataScraper twelveData,
    IOptions<AppSettings> settings,
    IHttpClientFactory httpFactory) : ControllerBase
{
    [HttpGet("test-scraper")]
    public async Task<IActionResult> TestScraper([FromQuery] string symbol = "OGDC")
    {
        var apiKey = settings.Value.TwelveDataApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return Ok(new { error = "TWELVE_DATA_API_KEY is not set in environment variables." });

        // Raw call so we can see the exact Twelve Data response
        var client = httpFactory.CreateClient();
        var url = $"https://api.twelvedata.com/time_series?symbol={symbol}:PSX&interval=1day&outputsize=5&apikey={apiKey}";
        var raw = await client.GetStringAsync(url);

        var results = await twelveData.FetchBatchAsync([symbol], 10);
        var prices = results.TryGetValue(symbol, out var p) ? p : [];
        return Ok(new
        {
            symbol,
            apiKeyConfigured = true,
            apiKeyPrefix = apiKey[..Math.Min(6, apiKey.Length)] + "...",
            twelveDataRawResponse = raw,
            recordsReturned = prices.Count
        });
    }


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
