using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Entities;

namespace PsXAiTool.Core.Interfaces;

public interface IClaudeService
{
    Task<List<ExtractedSignal>> ExtractSignalsAsync(string headline, string? content);
    Task<string> GenerateBriefingAsync(List<Signal> recentSignals, decimal? overallAccuracy);
}
