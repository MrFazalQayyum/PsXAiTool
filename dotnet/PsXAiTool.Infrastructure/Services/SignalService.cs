using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Enums;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Infrastructure.Data;

namespace PsXAiTool.Infrastructure.Services;

public class SignalService(
    AppDbContext db,
    IClaudeService claude,
    INewsScraperService scraper,
    IPrefilterService prefilter,
    INotificationService notifications,
    ILogger<SignalService> logger) : ISignalService
{
    public async Task<(IReadOnlyList<SignalDto> Items, int Total)> GetSignalsAsync(SignalFilterRequest filter)
    {
        var query = db.Signals.AsQueryable();

        if (filter.Direction.HasValue)
            query = query.Where(s => s.Direction == filter.Direction.Value);

        if (!string.IsNullOrWhiteSpace(filter.Ticker))
            query = query.Where(s => s.Entities.Contains(filter.Ticker));

        if (filter.MinConfidence.HasValue)
            query = query.Where(s => s.Confidence >= filter.MinConfidence.Value);

        var total = await query.CountAsync();

        var signals = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (signals.Select(MapToDto).ToList(), total);
    }

    public async Task<SignalStatsDto> GetStatsAsync()
    {
        var signals = await db.Signals.ToListAsync();
        var validations = await db.SignalValidations.ToListAsync();

        decimal? accuracy = null;
        if (validations.Count > 0)
        {
            var correct = validations.Count(v => v.Verdict == ValidationVerdict.Correct);
            accuracy = Math.Round((decimal)correct / validations.Count * 100, 1);
        }

        return new SignalStatsDto(
            signals.Count,
            signals.Count(s => s.Direction == SignalDirection.Bullish),
            signals.Count(s => s.Direction == SignalDirection.Bearish),
            signals.Count(s => s.Direction == SignalDirection.Neutral),
            accuracy);
    }

    public async Task<IReadOnlyList<ConflictDto>> GetConflictsAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var recent = await db.Signals.Where(s => s.CreatedAt >= cutoff).ToListAsync();

        var byTicker = recent
            .SelectMany(s => s.Entities.Select(e => (Ticker: e, Signal: s)))
            .GroupBy(x => x.Ticker)
            .Where(g =>
                g.Any(x => x.Signal.Direction == SignalDirection.Bullish) &&
                g.Any(x => x.Signal.Direction == SignalDirection.Bearish))
            .Select(g => new ConflictDto(
                g.Key,
                g.Where(x => x.Signal.Direction == SignalDirection.Bullish).Select(x => MapToDto(x.Signal)).ToList(),
                g.Where(x => x.Signal.Direction == SignalDirection.Bearish).Select(x => MapToDto(x.Signal)).ToList()))
            .ToList();

        return byTicker;
    }

    public async Task<IReadOnlyList<BriefingDto>> GetBriefingsAsync(int count = 10)
    {
        var briefings = await db.DailyBriefings
            .OrderByDescending(b => b.CreatedAt)
            .Take(count)
            .ToListAsync();

        return briefings.Select(b => new BriefingDto(b.Id, b.Content, b.SignalCount, b.AccuracyPct, b.CreatedAt)).ToList();
    }

    public async Task TriggerNewsProcessingAsync()
    {
        logger.LogInformation("Starting news processing pipeline...");

        var rssArticles = await scraper.ScrapeRssFeedsAsync();
        var psxArticles = await scraper.ScrapePsxAnnouncementsAsync();
        var all = rssArticles.Concat(psxArticles).ToList();

        logger.LogInformation("Scraped {Count} articles.", all.Count);

        foreach (var article in all)
        {
            var exists = await db.NewsArticles.AnyAsync(a => a.Url == article.Url);
            if (exists) continue;

            article.IsRelevant = prefilter.IsRelevant(article);
            db.NewsArticles.Add(article);
        }

        await db.SaveChangesAsync();

        var toProcess = all.Where(a => a.IsRelevant).ToList();
        logger.LogInformation("Processing {Count} relevant articles with Claude.", toProcess.Count);

        foreach (var article in toProcess)
        {
            var extracted = await claude.ExtractSignalsAsync(article.Title, article.Content);
            foreach (var e in extracted)
            {
                if (!Enum.TryParse<SignalDirection>(e.Direction, true, out var dir))
                    dir = SignalDirection.Neutral;

                var signal = new Signal
                {
                    NewsArticleId = article.Id,
                    SignalType = e.SignalType,
                    Entities = e.Entities,
                    Sectors = e.Sectors,
                    Direction = dir,
                    Confidence = e.Confidence,
                    Summary = e.Summary,
                    HistoricalNote = e.HistoricalNote,
                    RawHeadline = article.Title
                };

                db.Signals.Add(signal);

                if (signal.Confidence >= 0.65m)
                {
                    await notifications.SendNotificationAsync(
                        $"PSX Signal: {signal.Direction}",
                        signal.Summary,
                        "/signals");
                }
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("News processing complete.");
    }

    public async Task TriggerBriefingAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var recentSignals = await db.Signals.Where(s => s.CreatedAt >= cutoff).ToListAsync();

        var validations = await db.SignalValidations.ToListAsync();
        decimal? accuracy = null;
        if (validations.Count > 0)
        {
            var correct = validations.Count(v => v.Verdict == ValidationVerdict.Correct);
            accuracy = Math.Round((decimal)correct / validations.Count * 100, 1);
        }

        var content = await claude.GenerateBriefingAsync(recentSignals, accuracy);

        var briefing = new DailyBriefing
        {
            Content = content,
            SignalCount = recentSignals.Count,
            AccuracyPct = accuracy
        };
        db.DailyBriefings.Add(briefing);
        await db.SaveChangesAsync();

        await notifications.SendNotificationAsync("PSX Morning Briefing", content[..Math.Min(120, content.Length)], "/");
        logger.LogInformation("Daily briefing generated.");
    }

    public async Task TriggerValidationAsync()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var unvalidated = await db.Signals
            .Where(s => !db.SignalValidations.Any(v => v.SignalId == s.Id))
            .Where(s => DateOnly.FromDateTime(s.CreatedAt) < yesterday)
            .ToListAsync();

        logger.LogInformation("Validating {Count} signals.", unvalidated.Count);

        foreach (var signal in unvalidated)
        {
            foreach (var ticker in signal.Entities)
            {
                var signalDate = DateOnly.FromDateTime(signal.CreatedAt);
                var nextDay = signalDate.AddDays(1);

                var actual = await db.DailyPrices
                    .Where(p => p.Symbol == ticker && p.Date == nextDay && p.ChangePct.HasValue)
                    .Select(p => p.ChangePct)
                    .FirstOrDefaultAsync();

                if (actual is null) continue;

                var verdict = (signal.Direction, actual.Value) switch
                {
                    (SignalDirection.Bullish, > 0) => ValidationVerdict.Correct,
                    (SignalDirection.Bearish, < 0) => ValidationVerdict.Correct,
                    (SignalDirection.Neutral, _) => ValidationVerdict.Neutral,
                    _ => ValidationVerdict.Wrong
                };

                db.SignalValidations.Add(new SignalValidation
                {
                    SignalId = signal.Id,
                    Symbol = ticker,
                    ActualChange = actual.Value,
                    Verdict = verdict
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Signal validation complete.");
    }

    private static SignalDto MapToDto(Signal s) =>
        new(s.Id, s.SignalType, s.Entities, s.Sectors, s.Direction, s.Confidence, s.Summary, s.HistoricalNote, s.RawHeadline, s.CreatedAt);
}
