using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Settings;

namespace PsXAiTool.Infrastructure.Scrapers;

public class TwelveDataScraper(
    HttpClient httpClient,
    IOptions<AppSettings> settings,
    ILogger<TwelveDataScraper> logger)
{
    // Twelve Data free tier: 8 credits/minute, 800/day.
    // Batch up to 8 symbols per call; each symbol = 1 credit.
    private const int BatchSize = 8;
    private const int BatchDelayMs = 62_000; // 62s between batches to stay under 8/min

    public async Task<Dictionary<string, List<DailyPrice>>> FetchBatchAsync(
        IReadOnlyList<string> psxSymbols, int days = 100)
    {
        var result = new Dictionary<string, List<DailyPrice>>(StringComparer.OrdinalIgnoreCase);
        var apiKey = settings.Value.TwelveDataApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("TwelveDataApiKey is not configured.");
            return result;
        }

        var batches = psxSymbols
            .Select((s, i) => (s, i))
            .GroupBy(x => x.i / BatchSize)
            .Select(g => g.Select(x => x.s).ToList())
            .ToList();

        for (var bi = 0; bi < batches.Count; bi++)
        {
            if (bi > 0)
            {
                logger.LogInformation("Twelve Data: waiting 62s before next batch ({Batch}/{Total}).", bi + 1, batches.Count);
                await Task.Delay(BatchDelayMs);
            }

            var batch = batches[bi];
            var tdSymbols = string.Join(",", batch.Select(s => $"{s}:XKAR"));
            var url = $"https://api.twelvedata.com/time_series" +
                      $"?symbol={Uri.EscapeDataString(tdSymbols)}&interval=1day&outputsize={days}&apikey={apiKey}";

            try
            {
                var json = await httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (batch.Count == 1)
                {
                    // Single-symbol response: root has "values" directly
                    var prices = ParseSymbolValues(root, batch[0]);
                    if (prices.Count > 0) result[batch[0]] = prices;
                }
                else
                {
                    // Multi-symbol response: root keys are "OGDC:XKAR", "PPL:XKAR", …
                    foreach (var sym in batch)
                    {
                        var key = $"{sym}:XKAR";
                        if (!root.TryGetProperty(key, out var symEl)) continue;
                        var prices = ParseSymbolValues(symEl, sym);
                        if (prices.Count > 0) result[sym] = prices;
                    }
                }

                logger.LogInformation("Twelve Data batch {Batch}: fetched {Count}/{BatchSize} symbols.",
                    bi + 1, result.Count, batch.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Twelve Data batch {Batch} failed: {Error}", bi + 1, ex.Message);
            }
        }

        return result;
    }

    private static List<DailyPrice> ParseSymbolValues(JsonElement element, string psxSymbol)
    {
        // Check for API-level error inside this symbol's block
        if (element.TryGetProperty("status", out var statusEl) && statusEl.GetString() == "error")
            return new List<DailyPrice>();

        if (!element.TryGetProperty("values", out var valuesEl))
            return new List<DailyPrice>();

        var prices = new List<DailyPrice>();
        foreach (var v in valuesEl.EnumerateArray())
        {
            var dateStr = v.TryGetProperty("datetime", out var dtEl) ? dtEl.GetString() : null;
            if (!DateOnly.TryParseExact(dateStr, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            if (!TryGetDecimal(v, "close", out var close)) continue;
            TryGetDecimal(v, "open", out var open);
            TryGetDecimal(v, "high", out var high);
            TryGetDecimal(v, "low", out var low);
            TryGetLong(v, "volume", out var volume);

            prices.Add(new DailyPrice
            {
                Symbol = psxSymbol, Date = date,
                Open   = open  == 0 ? close : open,
                High   = high  == 0 ? close : high,
                Low    = low   == 0 ? close : low,
                Close  = close, Volume = volume
            });
        }

        // Twelve Data returns newest-first; reverse to chronological and compute ChangePct
        prices.Reverse();
        for (var i = 1; i < prices.Count; i++)
        {
            var prev = prices[i - 1].Close;
            if (prev != 0)
                prices[i].ChangePct = Math.Round((prices[i].Close - prev) / prev * 100, 4);
        }

        return prices;
    }

    private static bool TryGetDecimal(JsonElement el, string prop, out decimal value)
    {
        value = 0;
        if (!el.TryGetProperty(prop, out var p)) return false;
        if (p.ValueKind == JsonValueKind.Number) { value = p.GetDecimal(); return true; }
        return p.ValueKind == JsonValueKind.String &&
               decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetLong(JsonElement el, string prop, out long value)
    {
        value = 0;
        if (!el.TryGetProperty(prop, out var p)) return false;
        if (p.ValueKind == JsonValueKind.Number) { value = p.GetInt64(); return true; }
        return p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out value);
    }
}
