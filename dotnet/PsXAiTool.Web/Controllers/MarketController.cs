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

        var client = httpFactory.CreateClient();
        string raw1, raw2;

        // Format 1: symbol=OGDC%3APSX (colon encoded)
        var url1 = $"https://api.twelvedata.com/time_series?symbol={Uri.EscapeDataString(symbol + ":PSX")}&interval=1day&outputsize=5&apikey={apiKey}";
        try { raw1 = await client.GetStringAsync(url1); } catch (Exception ex) { raw1 = ex.Message; }

        // Format 2: symbol=OGDC&exchange=PSX (separate params)
        var url2 = $"https://api.twelvedata.com/time_series?symbol={symbol}&exchange=PSX&interval=1day&outputsize=5&apikey={apiKey}";
        try { raw2 = await client.GetStringAsync(url2); } catch (Exception ex) { raw2 = ex.Message; }

        return Ok(new
        {
            symbol,
            apiKeyPrefix = apiKey[..Math.Min(6, apiKey.Length)] + "...",
            format1_colonEncoded = raw1,
            format2_separateExchange = raw2
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
