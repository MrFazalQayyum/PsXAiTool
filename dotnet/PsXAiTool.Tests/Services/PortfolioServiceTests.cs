using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Entities;
using PsXAiTool.Infrastructure.Data;
using PsXAiTool.Infrastructure.Services;

namespace PsXAiTool.Tests.Services;

public class PortfolioServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PortfolioService _sut;

    public PortfolioServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new PortfolioService(_db);

        _db.Companies.Add(new Company { Symbol = "ENGRO", YahooTicker = "ENGRO.KA", Name = "Engro Corp", Sector = "Fertilizer", IsActive = true });
        _db.Companies.Add(new Company { Symbol = "LUCK", YahooTicker = "LUCK.KA", Name = "Lucky Cement", Sector = "Cement", IsActive = true });
        _db.SaveChanges();
    }

    [Fact]
    public async Task AddHolding_ValidRequest_CreatesAndReturns()
    {
        var request = new AddPortfolioRequest("ENGRO", 100, 350.50m, "Long term hold");
        var result = await _sut.AddHoldingAsync(request);

        result.Symbol.Should().Be("ENGRO");
        result.SharesHeld.Should().Be(100);
        result.AvgBuyPrice.Should().Be(350.50m);
        result.Notes.Should().Be("Long term hold");
    }

    [Fact]
    public async Task AddHolding_UnknownSymbol_ThrowsInvalidOperation()
    {
        var request = new AddPortfolioRequest("UNKNOWN", 100, 100m, null);
        await _sut.Invoking(s => s.AddHoldingAsync(request))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddHolding_DuplicateSymbol_ThrowsInvalidOperation()
    {
        await _sut.AddHoldingAsync(new AddPortfolioRequest("ENGRO", 100, 350m, null));
        await _sut.Invoking(s => s.AddHoldingAsync(new AddPortfolioRequest("ENGRO", 50, 360m, null)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateHolding_ExistingSymbol_UpdatesValues()
    {
        await _sut.AddHoldingAsync(new AddPortfolioRequest("ENGRO", 100, 350m, null));
        var result = await _sut.UpdateHoldingAsync("ENGRO", new UpdatePortfolioRequest(200, 375m, "Updated"));

        result.Should().NotBeNull();
        result!.SharesHeld.Should().Be(200);
        result.AvgBuyPrice.Should().Be(375m);
    }

    [Fact]
    public async Task UpdateHolding_MissingSymbol_ReturnsNull()
    {
        var result = await _sut.UpdateHoldingAsync("NOTFOUND", new UpdatePortfolioRequest(10, 100m, null));
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteHolding_ExistingSymbol_ReturnsTrueAndRemoves()
    {
        await _sut.AddHoldingAsync(new AddPortfolioRequest("LUCK", 50, 900m, null));
        var deleted = await _sut.DeleteHoldingAsync("LUCK");

        deleted.Should().BeTrue();
        var holdings = await _sut.GetHoldingsAsync();
        holdings.Should().NotContain(h => h.Symbol == "LUCK");
    }

    [Fact]
    public async Task DeleteHolding_MissingSymbol_ReturnsFalse()
    {
        var deleted = await _sut.DeleteHoldingAsync("GHOST");
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetHoldings_CalculatesPnL_WhenPriceAvailable()
    {
        await _sut.AddHoldingAsync(new AddPortfolioRequest("ENGRO", 100, 350m, null));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _db.DailyPrices.Add(new DailyPrice
        {
            Symbol = "ENGRO", Date = today,
            Open = 370, High = 375, Low = 365, Close = 370, Volume = 10000
        });
        await _db.SaveChangesAsync();

        var holdings = await _sut.GetHoldingsAsync();
        var engro = holdings.Single(h => h.Symbol == "ENGRO");

        engro.CurrentPrice.Should().Be(370m);
        engro.PnL.Should().Be((370m - 350m) * 100);
        engro.PnLPct.Should().BeApproximately((370m - 350m) / 350m * 100, 0.01m);
    }

    public void Dispose() => _db.Dispose();
}
