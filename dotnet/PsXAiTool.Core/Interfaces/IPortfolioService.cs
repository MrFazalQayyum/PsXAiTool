using PsXAiTool.Core.DTOs;

namespace PsXAiTool.Core.Interfaces;

public interface IPortfolioService
{
    Task<IReadOnlyList<PortfolioItemDto>> GetHoldingsAsync();
    Task<PortfolioItemDto> AddHoldingAsync(AddPortfolioRequest request);
    Task<PortfolioItemDto?> UpdateHoldingAsync(string symbol, UpdatePortfolioRequest request);
    Task<bool> DeleteHoldingAsync(string symbol);
}
