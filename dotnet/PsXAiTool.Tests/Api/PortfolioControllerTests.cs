using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Web.Controllers;

namespace PsXAiTool.Tests.Api;

public class PortfolioControllerTests
{
    private readonly Mock<IPortfolioService> _portfolio = new();
    private readonly PortfolioController _sut;

    public PortfolioControllerTests()
    {
        _sut = new PortfolioController(_portfolio.Object);
    }

    [Fact]
    public async Task GetHoldings_ReturnsOkWithList()
    {
        var expected = new List<PortfolioItemDto>
        {
            new("ENGRO", "Engro Corp", 100, 350m, 370m, 2000m, 5.71m, null)
        };
        _portfolio.Setup(p => p.GetHoldingsAsync()).ReturnsAsync(expected);

        var result = await _sut.GetHoldings();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task AddHolding_ValidRequest_ReturnsOk()
    {
        var request = new AddPortfolioRequest("ENGRO", 100, 350m, null);
        var dto = new PortfolioItemDto("ENGRO", "Engro Corp", 100, 350m, null, null, null, null);
        _portfolio.Setup(p => p.AddHoldingAsync(request)).ReturnsAsync(dto);

        var result = await _sut.AddHolding(request);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task AddHolding_DuplicateSymbol_ReturnsConflict()
    {
        var request = new AddPortfolioRequest("ENGRO", 100, 350m, null);
        _portfolio.Setup(p => p.AddHoldingAsync(request))
            .ThrowsAsync(new InvalidOperationException("Already exists."));

        var result = await _sut.AddHolding(request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateHolding_ExistingSymbol_ReturnsOk()
    {
        var req = new UpdatePortfolioRequest(200, 360m, "Updated");
        var dto = new PortfolioItemDto("ENGRO", "Engro Corp", 200, 360m, null, null, null, "Updated");
        _portfolio.Setup(p => p.UpdateHoldingAsync("ENGRO", req)).ReturnsAsync(dto);

        var result = await _sut.UpdateHolding("ENGRO", req);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateHolding_MissingSymbol_ReturnsNotFound()
    {
        _portfolio.Setup(p => p.UpdateHoldingAsync("GHOST", It.IsAny<UpdatePortfolioRequest>()))
            .ReturnsAsync((PortfolioItemDto?)null);

        var result = await _sut.UpdateHolding("GHOST", new UpdatePortfolioRequest(10, 100m, null));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteHolding_ExistingSymbol_ReturnsNoContent()
    {
        _portfolio.Setup(p => p.DeleteHoldingAsync("ENGRO")).ReturnsAsync(true);

        var result = await _sut.DeleteHolding("ENGRO");

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteHolding_MissingSymbol_ReturnsNotFound()
    {
        _portfolio.Setup(p => p.DeleteHoldingAsync("GHOST")).ReturnsAsync(false);

        var result = await _sut.DeleteHolding("GHOST");

        result.Should().BeOfType<NotFoundResult>();
    }
}
