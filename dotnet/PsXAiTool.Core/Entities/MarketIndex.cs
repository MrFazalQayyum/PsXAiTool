namespace PsXAiTool.Core.Entities;

public class MarketIndex
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string IndexName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? Change { get; set; }
    public decimal? ChangePct { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
