using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Infrastructure.Data;
using PsXAiTool.Infrastructure.Scrapers;

namespace PsXAiTool.Infrastructure.Services;

public class MarketService(
    AppDbContext db,
    YahooFinanceScraper yahooScraper,
    ILogger<MarketService> logger) : IMarketService
{
    public async Task<IReadOnlyList<IndexDto>> GetIndicesAsync()
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        // Materialize first; GroupBy + Select + First() cannot be translated to SQL by Npgsql 8
        var all = await db.MarketIndices
            .Where(i => i.Date >= cutoff)
            .OrderByDescending(i => i.Date)
            .ToListAsync();

        return all
            .GroupBy(i => i.IndexName)
            .Select(g => g.First())
            .OrderBy(i => i.IndexName)
            .Select(i => new IndexDto(i.IndexName, i.Value, i.Change, i.ChangePct, i.Date))
            .ToList();
    }

    public async Task<IReadOnlyList<TopMoverDto>> GetTopMoversAsync(int count = 5)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        var prices = await db.DailyPrices
            .Where(p => p.Date >= cutoff && p.ChangePct.HasValue)
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        var companies = await db.Companies.ToDictionaryAsync(c => c.Symbol);

        var latest = prices
            .GroupBy(p => p.Symbol)
            .Select(g => g.First())
            .Where(p => companies.ContainsKey(p.Symbol))
            .Select(p => new { p, c = companies[p.Symbol] })
            .ToList();

        var gainers = latest
            .OrderByDescending(x => x.p.ChangePct)
            .Take(count)
            .Select(x => new TopMoverDto(x.p.Symbol, x.c.Name, x.c.Sector, x.p.ChangePct!.Value, x.p.Close))
            .ToList();

        var losers = latest
            .OrderBy(x => x.p.ChangePct)
            .Take(count)
            .Select(x => new TopMoverDto(x.p.Symbol, x.c.Name, x.c.Sector, x.p.ChangePct!.Value, x.p.Close))
            .ToList();

        return gainers.Concat(losers).ToList();
    }

    public async Task<IReadOnlyList<SectorDto>> GetSectorsAsync()
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        var prices = await db.DailyPrices
            .Where(p => p.Date >= cutoff && p.ChangePct.HasValue)
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        var companies = await db.Companies.ToDictionaryAsync(c => c.Symbol);

        return prices
            .GroupBy(p => p.Symbol)
            .Select(g => g.First())
            .Where(p => companies.ContainsKey(p.Symbol))
            .Select(p => new { p, c = companies[p.Symbol] })
            .GroupBy(x => x.c.Sector)
            .Select(g => new SectorDto(
                g.Key,
                Math.Round(g.Average(x => x.p.ChangePct!.Value), 2),
                g.Count()))
            .OrderByDescending(s => s.AvgChangePct)
            .ToList();
    }

    public async Task<IReadOnlyList<StockDto>> GetStocksAsync()
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        var allPrices = await db.DailyPrices
            .Where(p => p.Date >= cutoff)
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        var priceMap = allPrices
            .GroupBy(p => p.Symbol)
            .ToDictionary(g => g.Key, g => g.First());

        var companies = await db.Companies.Where(c => c.IsActive).ToListAsync();

        return companies.Select(c =>
        {
            priceMap.TryGetValue(c.Symbol, out var price);
            return new StockDto(
                c.Symbol, c.Name, c.Sector,
                price?.Close, price?.ChangePct, price?.Volume, price?.Date);
        }).ToList();
    }

    public async Task<IReadOnlyList<PricePointDto>> GetStockPricesAsync(string symbol, int days = 90)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days);

        var prices = await db.DailyPrices
            .Where(p => p.Symbol == symbol && p.Date >= cutoff)
            .OrderBy(p => p.Date)
            .Select(p => new PricePointDto(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume))
            .ToListAsync();

        return prices;
    }

    public async Task<(int Companies, int PricesSaved)> FetchAndStorePricesAsync()
    {
        var companies = await db.Companies.Where(c => c.IsActive).ToListAsync();
        logger.LogInformation("Fetching prices for {Count} companies.", companies.Count);

        // Load all existing (symbol, date) keys in one query to avoid N+1 AnyAsync calls
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-120);
        var existingKeys = await db.DailyPrices
            .Where(p => p.Date >= cutoff)
            .Select(p => new { p.Symbol, p.Date })
            .ToListAsync();
        var existingSet = existingKeys.Select(x => $"{x.Symbol}:{x.Date}").ToHashSet();

        int saved = 0;
        int fetched = 0;

        foreach (var company in companies)
        {
            try
            {
                var prices = await yahooScraper.FetchPricesAsync(company.YahooTicker, company.Symbol);
                fetched += prices.Count;
                foreach (var price in prices)
                {
                    var key = $"{price.Symbol}:{price.Date}";
                    if (!existingSet.Contains(key))
                    {
                        db.DailyPrices.Add(price);
                        existingSet.Add(key);
                        saved++;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Price fetch failed for {Symbol}: {Error}", company.Symbol, ex.Message);
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Saved {Saved} new price records from {Fetched} fetched.", saved, fetched);

        await FetchIndicesAsync();
        return (companies.Count, saved);
    }

    private async Task FetchIndicesAsync()
    {
        var indexTickers = new Dictionary<string, string>
        {
            ["^KSE100"] = "KSE-100",
            ["^KSE30"] = "KSE-30",
            ["^KMI30"] = "KMI-30"
        };

        foreach (var (ticker, name) in indexTickers)
        {
            try
            {
                var index = await yahooScraper.FetchIndexAsync(ticker, name);
                if (index is null) continue;

                var exists = await db.MarketIndices.AnyAsync(i => i.Date == index.Date && i.IndexName == index.IndexName);
                if (!exists) db.MarketIndices.Add(index);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Index fetch failed for {Name}: {Error}", name, ex.Message);
            }
        }

        await db.SaveChangesAsync();
    }
}
