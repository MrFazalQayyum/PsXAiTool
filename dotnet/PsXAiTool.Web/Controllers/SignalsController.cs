using Microsoft.AspNetCore.Mvc;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Enums;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Web.Controllers;

[ApiController]
[Route("api/signals")]
public class SignalsController(ISignalService signals) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSignals(
        [FromQuery] SignalDirection? direction,
        [FromQuery] string? ticker,
        [FromQuery] decimal? minConfidence,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var filter = new SignalFilterRequest(direction, ticker, minConfidence, page, pageSize);
        var (items, total) = await signals.GetSignalsAsync(filter);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats() => Ok(await signals.GetStatsAsync());

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts() => Ok(await signals.GetConflictsAsync());

    [HttpGet("briefings")]
    public async Task<IActionResult> GetBriefings([FromQuery] int count = 10) =>
        Ok(await signals.GetBriefingsAsync(count));

    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerProcessing()
    {
        await signals.TriggerNewsProcessingAsync();
        return Ok(new { message = "News processing triggered." });
    }

    [HttpPost("briefing/trigger")]
    public async Task<IActionResult> TriggerBriefing()
    {
        await signals.TriggerBriefingAsync();
        return Ok(new { message = "Briefing generation triggered." });
    }

    [HttpPost("validate/trigger")]
    public async Task<IActionResult> TriggerValidation()
    {
        await signals.TriggerValidationAsync();
        return Ok(new { message = "Signal validation triggered." });
    }
}
