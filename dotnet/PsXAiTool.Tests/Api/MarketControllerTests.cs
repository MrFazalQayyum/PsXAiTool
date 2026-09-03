using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Web.Controllers;

namespace PsXAiTool.Tests.Api;

public class MarketControllerTests
{
    private readonly Mock<IMarketService> _market = new();
    private readonly MarketController _sut;

    public MarketControllerTests()
    {
        _sut = new MarketController(_market.Object);
    }

    [Fact]
    public async Task GetIndices_ReturnsOkWithIndices()
    {
        var expected = new List<IndexDto>
        {
            new("KSE-100", 95000m, 300m, 0.32m, DateOnly.FromDateTime(DateTime.UtcNow))
        };
        _market.Setup(m => m.GetIndicesAsync()).ReturnsAsync(expected);

        var result = await _sut.GetIndices();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetTopMovers_DefaultCount_CallsServiceWithFive()
    {
        _market.Setup(m => m.GetTopMoversAsync(5)).ReturnsAsync(new List<TopMoverDto>());

        await _sut.GetTopMovers();

        _market.Verify(m => m.GetTopMoversAsync(5), Times.Once);
    }

    [Fact]
    public async Task GetSectors_ReturnsOkWithSectors()
    {
        var expected = new List<SectorDto> { new("Cement", 1.5m, 4) };
        _market.Setup(m => m.GetSectorsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetSectors();

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetStocks_ReturnsAllStocks()
    {
        var expected = new List<StockDto>
        {
            new("ENGRO", "Engro Corp", "Fertilizer", 350m, 1.2m, 100000, DateOnly.FromDateTime(DateTime.UtcNow))
        };
        _market.Setup(m => m.GetStocksAsync()).ReturnsAsync(expected);

        var result = await _sut.GetStocks();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetStockPrices_ReturnsOkWithPricePoints()
    {
        var prices = new List<PricePointDto>
        {
            new(DateOnly.FromDateTime(DateTime.UtcNow), 348m, 355m, 345m, 350m, 50000)
        };
        _market.Setup(m => m.GetStockPricesAsync("ENGRO", 90)).ReturnsAsync(prices);

        var result = await _sut.GetStockPrices("ENGRO");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(prices);
    }

    [Fact]
    public async Task TriggerFetch_CallsFetchAndReturnsOk()
    {
        _market.Setup(m => m.FetchAndStorePricesAsync()).ReturnsAsync((5, 100));

        var result = await _sut.TriggerFetch();

        _market.Verify(m => m.FetchAndStorePricesAsync(), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }
}
