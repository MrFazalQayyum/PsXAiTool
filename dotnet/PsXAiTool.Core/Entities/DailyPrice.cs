namespace PsXAiTool.Core.Entities;

public class DailyPrice
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? ChangePct { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
}
