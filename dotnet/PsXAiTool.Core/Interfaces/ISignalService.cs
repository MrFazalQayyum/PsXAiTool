using PsXAiTool.Core.DTOs;

namespace PsXAiTool.Core.Interfaces;

public interface ISignalService
{
    Task<(IReadOnlyList<SignalDto> Items, int Total)> GetSignalsAsync(SignalFilterRequest filter);
    Task<SignalStatsDto> GetStatsAsync();
    Task<IReadOnlyList<ConflictDto>> GetConflictsAsync();
    Task<IReadOnlyList<BriefingDto>> GetBriefingsAsync(int count = 10);
    Task TriggerNewsProcessingAsync();
    Task TriggerBriefingAsync();
    Task TriggerValidationAsync();
}
