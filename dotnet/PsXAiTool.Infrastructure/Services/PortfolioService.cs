using Microsoft.EntityFrameworkCore;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Infrastructure.Data;

namespace PsXAiTool.Infrastructure.Services;

public class PortfolioService(AppDbContext db) : IPortfolioService
{
    public async Task<IReadOnlyList<PortfolioItemDto>> GetHoldingsAsync()
    {
        var holdings = await db.Portfolio
            .Join(db.Companies, p => p.Symbol, c => c.Symbol, (p, c) => new { p, c })
            .ToListAsync();

        var symbols = holdings.Select(h => h.p.Symbol).ToList();
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);

        var latestPrices = await db.DailyPrices
            .Where(dp => symbols.Contains(dp.Symbol) && dp.Date >= cutoff)
            .GroupBy(dp => dp.Symbol)
            .Select(g => g.OrderByDescending(p => p.Date).First())
            .ToDictionaryAsync(p => p.Symbol);

        return holdings.Select(h =>
        {
            latestPrices.TryGetValue(h.p.Symbol, out var price);
            var currentPrice = price?.Close;
            var pnl = currentPrice.HasValue ? (currentPrice.Value - h.p.AvgBuyPrice) * h.p.SharesHeld : (decimal?)null;
            var pnlPct = currentPrice.HasValue && h.p.AvgBuyPrice > 0
                ? (currentPrice.Value - h.p.AvgBuyPrice) / h.p.AvgBuyPrice * 100
                : (decimal?)null;

            return new PortfolioItemDto(
                h.p.Symbol, h.c.Name, h.p.SharesHeld, h.p.AvgBuyPrice,
                currentPrice, pnl, pnlPct, h.p.Notes);
        }).ToList();
    }

    public async Task<PortfolioItemDto> AddHoldingAsync(AddPortfolioRequest request)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == request.Symbol)
            ?? throw new InvalidOperationException($"Company {request.Symbol} not found.");

        var existing = await db.Portfolio.FirstOrDefaultAsync(p => p.Symbol == request.Symbol);
        if (existing is not null)
            throw new InvalidOperationException($"Holding for {request.Symbol} already exists. Use update instead.");

        var holding = new Portfolio
        {
            Symbol = request.Symbol,
            SharesHeld = request.SharesHeld,
            AvgBuyPrice = request.AvgBuyPrice,
            Notes = request.Notes
        };

        db.Portfolio.Add(holding);
        await db.SaveChangesAsync();

        return new PortfolioItemDto(company.Symbol, company.Name, holding.SharesHeld, holding.AvgBuyPrice, null, null, null, holding.Notes);
    }

    public async Task<PortfolioItemDto?> UpdateHoldingAsync(string symbol, UpdatePortfolioRequest request)
    {
        var holding = await db.Portfolio.FirstOrDefaultAsync(p => p.Symbol == symbol);
        if (holding is null) return null;

        holding.SharesHeld = request.SharesHeld;
        holding.AvgBuyPrice = request.AvgBuyPrice;
        holding.Notes = request.Notes;
        holding.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol);
        return new PortfolioItemDto(symbol, company?.Name ?? symbol, holding.SharesHeld, holding.AvgBuyPrice, null, null, null, holding.Notes);
    }

    public async Task<bool> DeleteHoldingAsync(string symbol)
    {
        var holding = await db.Portfolio.FirstOrDefaultAsync(p => p.Symbol == symbol);
        if (holding is null) return false;

        db.Portfolio.Remove(holding);
        await db.SaveChangesAsync();
        return true;
    }
}
