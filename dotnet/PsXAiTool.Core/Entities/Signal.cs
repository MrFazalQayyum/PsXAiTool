using PsXAiTool.Core.Enums;

namespace PsXAiTool.Core.Entities;

public class Signal
{
    public int Id { get; set; }
    public int? NewsArticleId { get; set; }
    public string SignalType { get; set; } = string.Empty;
    public List<string> Entities { get; set; } = new();
    public List<string> Sectors { get; set; } = new();
    public SignalDirection Direction { get; set; }
    public decimal Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? HistoricalNote { get; set; }
    public string RawHeadline { get; set; } = string.Empty;
    public bool IsNotified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public NewsArticle? NewsArticle { get; set; }
    public SignalValidation? Validation { get; set; }
}
