using PsXAiTool.Core.DTOs;

namespace PsXAiTool.Core.Interfaces;

public interface IMarketService
{
    Task<IReadOnlyList<IndexDto>> GetIndicesAsync();
    Task<IReadOnlyList<TopMoverDto>> GetTopMoversAsync(int count = 5);
    Task<IReadOnlyList<SectorDto>> GetSectorsAsync();
    Task<IReadOnlyList<StockDto>> GetStocksAsync();
    Task<IReadOnlyList<PricePointDto>> GetStockPricesAsync(string symbol, int days = 90);
    Task FetchAndStorePricesAsync();
}
