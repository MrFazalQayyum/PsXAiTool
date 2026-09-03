using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PsXAiTool.Core.Entities;

namespace PsXAiTool.Infrastructure.Scrapers;

public class YahooFinanceScraper(HttpClient httpClient, ILogger<YahooFinanceScraper> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<DailyPrice>> FetchPricesAsync(string yahooTicker, string psxSymbol, int days = 100)
    {
        // Try stooq.com first — more reliable from cloud servers for PSX stocks
        var stooqTicker = $"{psxSymbol.ToLower()}.pk";
        var stooqPrices = await FetchFromStooqAsync(stooqTicker, psxSymbol, days);
        if (stooqPrices.Count > 0)
            return stooqPrices;

        // Fall back to Yahoo Finance
        return await FetchFromYahooAsync(yahooTicker, psxSymbol, days);
    }

    private async Task<List<DailyPrice>> FetchFromStooqAsync(string stooqTicker, string psxSymbol, int days)
    {
        // stooq returns a CSV: Date,Open,High,Low,Close,Volume
        var url = $"https://stooq.com/q/d/l/?s={Uri.EscapeDataString(stooqTicker)}&i=d";
        try
        {
            var csv = await httpClient.GetStringAsync(url);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return new List<DailyPrice>();

            var cutoff = DateTime.UtcNow.AddDays(-days);
            var prices = new List<DailyPrice>();

            // Skip header row; stooq returns newest-first so we collect then reverse
            foreach (var line in lines.Skip(1))
            {
                var cols = line.Trim().Split(',');
                if (cols.Length < 5) continue;
                if (!DateOnly.TryParseExact(cols[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date)) continue;
                if (date.ToDateTime(TimeOnly.MinValue) < cutoff) continue;

                if (!decimal.TryParse(cols[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var open)) continue;
                if (!decimal.TryParse(cols[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var high)) continue;
                if (!decimal.TryParse(cols[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var low)) continue;
                if (!decimal.TryParse(cols[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var close)) continue;
                long.TryParse(cols.Length > 5 ? cols[5].Trim() : "0", out var volume);

                prices.Add(new DailyPrice
                {
                    Date = date, Symbol = psxSymbol,
                    Open = open, High = high, Low = low, Close = close, Volume = volume
                });
            }

            // stooq returns newest-first; reverse to chronological order and compute ChangePct
            prices.Reverse();
            for (var i = 1; i < prices.Count; i++)
            {
                var prev = prices[i - 1].Close;
                if (prev != 0)
                    prices[i].ChangePct = Math.Round((prices[i].Close - prev) / prev * 100, 4);
            }

            return prices;
        }
        catch (Exception ex)
        {
            logger.LogDebug("Stooq fetch failed for {Ticker}: {Error}", stooqTicker, ex.Message);
            return new List<DailyPrice>();
        }
    }

    private async Task<List<DailyPrice>> FetchFromYahooAsync(string yahooTicker, string psxSymbol, int days)
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

            var opens   = GetDecimals(indicators, "open");
            var highs   = GetDecimals(indicators, "high");
            var lows    = GetDecimals(indicators, "low");
            var closes  = GetDecimals(indicators, "close");
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
                    Date = date, Symbol = psxSymbol,
                    Open    = opens[i]   ?? closes[i]!.Value,
                    High    = highs[i]   ?? closes[i]!.Value,
                    Low     = lows[i]    ?? closes[i]!.Value,
                    Close   = closes[i]!.Value,
                    Volume  = volumes[i] ?? 0,
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
            Date      = latest.Date,
            IndexName = indexName,
            Value     = latest.Close,
            Change    = prices.Count >= 2 ? latest.Close - prices[^2].Close : null,
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
