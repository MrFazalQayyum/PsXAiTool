using System.Text.Json;
using Microsoft.Extensions.Logging;
using PsXAiTool.Core.Entities;

namespace PsXAiTool.Infrastructure.Scrapers;

public class YahooFinanceScraper(HttpClient httpClient, ILogger<YahooFinanceScraper> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<DailyPrice>> FetchPricesAsync(string yahooTicker, string psxSymbol, int days = 100)
    {
        var period2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var period1 = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();

        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(yahooTicker)}" +
                  $"?interval=1d&period1={period1}&period2={period2}";

        try
        {
            var response = await httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var timestamps = result.GetProperty("timestamp").EnumerateArray().Select(t => t.GetInt64()).ToList();
            var indicators = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens = GetDecimals(indicators, "open");
            var highs = GetDecimals(indicators, "high");
            var lows = GetDecimals(indicators, "low");
            var closes = GetDecimals(indicators, "close");
            var volumes = GetLongs(indicators, "volume");

            var prices = new List<DailyPrice>();
            for (var i = 0; i < timestamps.Count; i++)
            {
                if (closes[i] is null) continue;

                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime);
                var changePct = i > 0 && closes[i - 1].HasValue && closes[i - 1] != 0
                    ? Math.Round((closes[i]!.Value - closes[i - 1]!.Value) / closes[i - 1]!.Value * 100, 4)
                    : (decimal?)null;

                prices.Add(new DailyPrice
                {
                    Date = date,
                    Symbol = psxSymbol,
                    Open = opens[i] ?? closes[i]!.Value,
                    High = highs[i] ?? closes[i]!.Value,
                    Low = lows[i] ?? closes[i]!.Value,
                    Close = closes[i]!.Value,
                    Volume = volumes[i] ?? 0,
                    ChangePct = changePct
                });
            }

            return prices;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Yahoo Finance fetch failed for {Ticker}: {Error}", yahooTicker, ex.Message);
            return new List<DailyPrice>();
        }
    }

    public async Task<MarketIndex?> FetchIndexAsync(string yahooTicker, string indexName)
    {
        var prices = await FetchPricesAsync(yahooTicker, indexName, 5);
        var latest = prices.LastOrDefault();
        if (latest is null) return null;

        return new MarketIndex
        {
            Date = latest.Date,
            IndexName = indexName,
            Value = latest.Close,
            Change = prices.Count >= 2 ? latest.Close - prices[^2].Close : null,
            ChangePct = latest.ChangePct
        };
    }

    private static List<decimal?> GetDecimals(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var arr)) return new();
        return arr.EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.Number ? (decimal?)v.GetDecimal() : null)
            .ToList();
    }

    private static List<long?> GetLongs(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var arr)) return new();
        return arr.EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.Number ? (long?)v.GetInt64() : null)
            .ToList();
    }
}
