using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Enums;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Infrastructure.Data;
using PsXAiTool.Infrastructure.Services;

namespace PsXAiTool.Tests.Services;

public class SignalServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SignalService _sut;
    private readonly Mock<IClaudeService> _claude = new();
    private readonly Mock<INewsScraperService> _scraper = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IPrefilterService> _prefilter = new();
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<SignalService>> _logger = new();

    public SignalServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _sut = new SignalService(_db, _claude.Object, _scraper.Object, _prefilter.Object, _notifications.Object, _logger.Object);
    }

    [Fact]
    public async Task GetStats_NoSignals_ReturnsZeroes()
    {
        var stats = await _sut.GetStatsAsync();

        stats.Total.Should().Be(0);
        stats.Bullish.Should().Be(0);
        stats.AccuracyPct.Should().BeNull();
    }

    [Fact]
    public async Task GetStats_WithSignals_CountsCorrectly()
    {
        _db.Signals.AddRange(
            new Signal { Direction = SignalDirection.Bullish, SignalType = "Test", Summary = "S1", RawHeadline = "H1", Confidence = 0.8m, Entities = new(), Sectors = new() },
            new Signal { Direction = SignalDirection.Bullish, SignalType = "Test", Summary = "S2", RawHeadline = "H2", Confidence = 0.7m, Entities = new(), Sectors = new() },
            new Signal { Direction = SignalDirection.Bearish, SignalType = "Test", Summary = "S3", RawHeadline = "H3", Confidence = 0.6m, Entities = new(), Sectors = new() }
        );
        await _db.SaveChangesAsync();

        var stats = await _sut.GetStatsAsync();

        stats.Total.Should().Be(3);
        stats.Bullish.Should().Be(2);
        stats.Bearish.Should().Be(1);
        stats.Neutral.Should().Be(0);
    }

    [Fact]
    public async Task GetStats_WithValidations_ComputesAccuracy()
    {
        var sig1 = new Signal { Direction = SignalDirection.Bullish, SignalType = "T", Summary = "S", RawHeadline = "H", Confidence = 0.8m, Entities = new(), Sectors = new() };
        var sig2 = new Signal { Direction = SignalDirection.Bearish, SignalType = "T", Summary = "S", RawHeadline = "H", Confidence = 0.7m, Entities = new(), Sectors = new() };
        _db.Signals.AddRange(sig1, sig2);
        await _db.SaveChangesAsync();

        _db.SignalValidations.Add(new SignalValidation { SignalId = sig1.Id, Symbol = "ENGRO", Verdict = ValidationVerdict.Correct });
        _db.SignalValidations.Add(new SignalValidation { SignalId = sig2.Id, Symbol = "LUCK", Verdict = ValidationVerdict.Wrong });
        await _db.SaveChangesAsync();

        var stats = await _sut.GetStatsAsync();
        stats.AccuracyPct.Should().Be(50m);
    }

    [Fact]
    public async Task GetConflicts_BothDirectionsForSameTicker_ReturnsConflict()
    {
        var now = DateTime.UtcNow;
        _db.Signals.AddRange(
            new Signal { Direction = SignalDirection.Bullish, Entities = new List<string> { "ENGRO" }, Summary = "up", SignalType = "T", RawHeadline = "H", Confidence = 0.8m, Sectors = new(), CreatedAt = now },
            new Signal { Direction = SignalDirection.Bearish, Entities = new List<string> { "ENGRO" }, Summary = "down", SignalType = "T", RawHeadline = "H", Confidence = 0.7m, Sectors = new(), CreatedAt = now }
        );
        await _db.SaveChangesAsync();

        var conflicts = await _sut.GetConflictsAsync();
        conflicts.Should().ContainSingle(c => c.Ticker == "ENGRO");
        conflicts[0].BullishSignals.Should().HaveCount(1);
        conflicts[0].BearishSignals.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetConflicts_OldSignals_NotIncluded()
    {
        var old = DateTime.UtcNow.AddHours(-25);
        _db.Signals.AddRange(
            new Signal { Direction = SignalDirection.Bullish, Entities = new List<string> { "LUCK" }, Summary = "up", SignalType = "T", RawHeadline = "H", Confidence = 0.8m, Sectors = new(), CreatedAt = old },
            new Signal { Direction = SignalDirection.Bearish, Entities = new List<string> { "LUCK" }, Summary = "down", SignalType = "T", RawHeadline = "H", Confidence = 0.7m, Sectors = new(), CreatedAt = old }
        );
        await _db.SaveChangesAsync();

        var conflicts = await _sut.GetConflictsAsync();
        conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSignals_FilterByDirection_ReturnsMatching()
    {
        _db.Signals.AddRange(
            new Signal { Direction = SignalDirection.Bullish, SignalType = "T", Summary = "S", RawHeadline = "H", Confidence = 0.8m, Entities = new(), Sectors = new() },
            new Signal { Direction = SignalDirection.Bearish, SignalType = "T", Summary = "S", RawHeadline = "H", Confidence = 0.7m, Entities = new(), Sectors = new() }
        );
        await _db.SaveChangesAsync();

        var filter = new PsXAiTool.Core.DTOs.SignalFilterRequest(PsXAiTool.Core.Enums.SignalDirection.Bullish);
        var (items, total) = await _sut.GetSignalsAsync(filter);

        total.Should().Be(1);
        items.Should().AllSatisfy(s => s.Direction.Should().Be(SignalDirection.Bullish));
    }

    [Fact]
    public async Task TriggerValidation_BullishSignalWithPositiveMove_MarksCorrect()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        var signal = new Signal
        {
            Direction = SignalDirection.Bullish,
            Entities = new List<string> { "ENGRO" },
            SignalType = "Test",
            Summary = "S",
            RawHeadline = "H",
            Confidence = 0.8m,
            Sectors = new(),
            CreatedAt = yesterday.ToDateTime(TimeOnly.MinValue)
        };
        _db.Signals.Add(signal);
        await _db.SaveChangesAsync();

        var nextDay = yesterday.AddDays(1);
        _db.DailyPrices.Add(new DailyPrice
        {
            Symbol = "ENGRO", Date = nextDay,
            Open = 350, High = 360, Low = 348, Close = 358, Volume = 5000, ChangePct = 2.3m
        });
        await _db.SaveChangesAsync();

        await _sut.TriggerValidationAsync();

        var validation = await _db.SignalValidations.FirstOrDefaultAsync(v => v.SignalId == signal.Id);
        validation.Should().NotBeNull();
        validation!.Verdict.Should().Be(ValidationVerdict.Correct);
    }

    [Fact]
    public async Task TriggerValidation_BearishSignalWithPositiveMove_MarksWrong()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        var signal = new Signal
        {
            Direction = SignalDirection.Bearish,
            Entities = new List<string> { "LUCK" },
            SignalType = "Test",
            Summary = "S",
            RawHeadline = "H",
            Confidence = 0.7m,
            Sectors = new(),
            CreatedAt = yesterday.ToDateTime(TimeOnly.MinValue)
        };
        _db.Signals.Add(signal);
        await _db.SaveChangesAsync();

        var nextDay = yesterday.AddDays(1);
        _db.DailyPrices.Add(new DailyPrice
        {
            Symbol = "LUCK", Date = nextDay,
            Open = 900, High = 920, Low = 895, Close = 915, Volume = 3000, ChangePct = 1.7m
        });
        await _db.SaveChangesAsync();

        await _sut.TriggerValidationAsync();

        var validation = await _db.SignalValidations.FirstOrDefaultAsync(v => v.SignalId == signal.Id);
        validation!.Verdict.Should().Be(ValidationVerdict.Wrong);
    }

    public void Dispose() => _db.Dispose();
}
