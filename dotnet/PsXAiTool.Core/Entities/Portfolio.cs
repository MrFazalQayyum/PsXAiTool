namespace PsXAiTool.Core.Entities;

public class Portfolio
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal SharesHeld { get; set; }
    public decimal AvgBuyPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
}
