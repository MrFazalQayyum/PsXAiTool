using PsXAiTool.Core.Enums;

namespace PsXAiTool.Core.DTOs;

public record SignalDto(
    int Id,
    string SignalType,
    List<string> Entities,
    List<string> Sectors,
    SignalDirection Direction,
    decimal Confidence,
    string Summary,
    string? HistoricalNote,
    string RawHeadline,
    DateTime CreatedAt);

public record SignalStatsDto(int Total, int Bullish, int Bearish, int Neutral, decimal? AccuracyPct);

public record ConflictDto(string Ticker, List<SignalDto> BullishSignals, List<SignalDto> BearishSignals);

public record BriefingDto(int Id, string Content, int SignalCount, decimal? AccuracyPct, DateTime CreatedAt);

public record SignalFilterRequest(
    SignalDirection? Direction = null,
    string? Ticker = null,
    decimal? MinConfidence = null,
    int Page = 1,
    int PageSize = 20);

public record ExtractedSignal(
    string SignalType,
    List<string> Entities,
    List<string> Sectors,
    string Direction,
    decimal Confidence,
    string Summary,
    string? HistoricalNote);
