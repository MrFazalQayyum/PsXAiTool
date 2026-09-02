using PsXAiTool.Core.Enums;

namespace PsXAiTool.Core.Entities;

public class SignalValidation
{
    public int Id { get; set; }
    public int SignalId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal? PredictedChange { get; set; }
    public decimal? ActualChange { get; set; }
    public ValidationVerdict Verdict { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    public Signal Signal { get; set; } = null!;
}
