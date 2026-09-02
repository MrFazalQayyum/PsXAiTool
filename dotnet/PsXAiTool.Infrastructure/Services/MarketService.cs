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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(-7);

        var indices = await db.MarketIndices
            .Where(i => i.Date >= cutoff)
            .GroupBy(i => i.IndexName)
            .Select(g => g.OrderByDescending(i => i.Date).First())
            .OrderBy(i => i.IndexName)
            .ToListAsync();

        return indices.Select(i => new IndexDto(i.IndexName, i.Value, i.Change, i.ChangePct, i.Date)).ToList();
    }

    public async Task<IReadOnlyList<TopMoverDto>> GetTopMoversAsync(int count = 5)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        var latest = await db.DailyPrices
            .Where(p => p.Date >= cutoff && p.ChangePct.HasValue)
            .GroupBy(p => p.Symbol)
            .Select(g => g.OrderByDescending(p => p.Date).First())
            .Join(db.Companies, p => p.Symbol, c => c.Symbol, (p, c) => new { p, c })
            .ToListAsync();

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

        var latest = await db.DailyPrices
            .Where(p => p.Date >= cutoff && p.ChangePct.HasValue)
            .GroupBy(p => p.Symbol)
            .Select(g => g.OrderByDescending(p => p.Date).First())
            .Join(db.Companies, p => p.Symbol, c => c.Symbol, (p, c) => new { p, c })
            .ToListAsync();

        return latest
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

        var latestPrices = await db.DailyPrices
            .Where(p => p.Date >= cutoff)
            .GroupBy(p => p.Symbol)
            .Select(g => g.OrderByDescending(p => p.Date).First())
            .ToListAsync();

        var priceMap = latestPrices.ToDictionary(p => p.Symbol);

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

    public async Task FetchAndStorePricesAsync()
    {
        var companies = await db.Companies.Where(c => c.IsActive).ToListAsync();
        logger.LogInformation("Fetching prices for {Count} companies.", companies.Count);

        foreach (var company in companies)
        {
            try
            {
                var prices = await yahooScraper.FetchPricesAsync(company.YahooTicker, company.Symbol);
                foreach (var price in prices)
                {
                    var exists = await db.DailyPrices.AnyAsync(p => p.Date == price.Date && p.Symbol == price.Symbol);
                    if (!exists) db.DailyPrices.Add(price);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Price fetch failed for {Symbol}: {Error}", company.Symbol, ex.Message);
            }
        }

        await db.SaveChangesAsync();

        await FetchIndicesAsync();
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
