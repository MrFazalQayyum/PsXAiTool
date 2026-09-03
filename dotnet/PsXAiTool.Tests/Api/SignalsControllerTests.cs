using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Enums;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Web.Controllers;

namespace PsXAiTool.Tests.Api;

public class SignalsControllerTests
{
    private readonly Mock<ISignalService> _signals = new();
    private readonly SignalsController _sut;

    public SignalsControllerTests()
    {
        _sut = new SignalsController(_signals.Object);
    }

    [Fact]
    public async Task GetSignals_NoFilter_ReturnsOkWithItems()
    {
        var items = new List<SignalDto>
        {
            new(1, "EarningsReport", new(), new(), SignalDirection.Bullish, 0.8m, "Summary", null, "Headline", DateTime.UtcNow)
        };
        _signals.Setup(s => s.GetSignalsAsync(It.IsAny<SignalFilterRequest>())).ReturnsAsync((items, 1));

        var result = await _sut.GetSignals(null, null, null);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetStats_ReturnsOkWithStats()
    {
        var stats = new SignalStatsDto(10, 6, 3, 1, 60m);
        _signals.Setup(s => s.GetStatsAsync()).ReturnsAsync(stats);

        var result = await _sut.GetStats();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(stats);
    }

    [Fact]
    public async Task GetConflicts_ReturnsOkList()
    {
        _signals.Setup(s => s.GetConflictsAsync()).ReturnsAsync(new List<ConflictDto>());

        var result = await _sut.GetConflicts();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TriggerProcessing_CallsServiceAndReturnsOk()
    {
        _signals.Setup(s => s.TriggerNewsProcessingAsync()).Returns(Task.CompletedTask);

        var result = await _sut.TriggerProcessing();

        _signals.Verify(s => s.TriggerNewsProcessingAsync(), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TriggerBriefing_CallsServiceAndReturnsOk()
    {
        _signals.Setup(s => s.TriggerBriefingAsync()).Returns(Task.CompletedTask);

        var result = await _sut.TriggerBriefing();

        _signals.Verify(s => s.TriggerBriefingAsync(), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TriggerValidation_CallsServiceAndReturnsOk()
    {
        _signals.Setup(s => s.TriggerValidationAsync()).Returns(Task.CompletedTask);

        var result = await _sut.TriggerValidation();

        _signals.Verify(s => s.TriggerValidationAsync(), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }
}
