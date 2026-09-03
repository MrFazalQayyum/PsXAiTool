namespace PsXAiTool.Core.Entities;

public class NewsArticle
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public DateTime PublishedAt { get; set; }
    public bool IsRelevant { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Signal> Signals { get; set; } = new List<Signal>();
}
