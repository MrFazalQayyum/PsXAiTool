namespace PsXAiTool.Core.Entities;

public class Company
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string YahooTicker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal? MarketCap { get; set; }
    public long? SharesOutstanding { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DailyPrice> DailyPrices { get; set; } = new List<DailyPrice>();
    public ICollection<Portfolio> PortfolioHoldings { get; set; } = new List<Portfolio>();
}
