namespace PsXAiTool.Core.DTOs;

public record IndexDto(string Name, decimal Value, decimal? Change, decimal? ChangePct, DateOnly Date);

public record StockDto(
    string Symbol,
    string Name,
    string Sector,
    decimal? LastClose,
    decimal? ChangePct,
    long? Volume,
    DateOnly? LastDate);

public record TopMoverDto(string Symbol, string Name, string Sector, decimal ChangePct, decimal Close);

public record SectorDto(string Sector, decimal AvgChangePct, int CompanyCount);

public record PricePointDto(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

public record PortfolioItemDto(
    string Symbol,
    string Name,
    decimal SharesHeld,
    decimal AvgBuyPrice,
    decimal? CurrentPrice,
    decimal? PnL,
    decimal? PnLPct,
    string? Notes);

public record AddPortfolioRequest(string Symbol, decimal SharesHeld, decimal AvgBuyPrice, string? Notes);

public record UpdatePortfolioRequest(decimal SharesHeld, decimal AvgBuyPrice, string? Notes);
