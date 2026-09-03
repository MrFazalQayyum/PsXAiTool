namespace PsXAiTool.Core.Entities;

public class DailyBriefing
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int SignalCount { get; set; }
    public decimal? AccuracyPct { get; set; }
    public bool IsPushed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
