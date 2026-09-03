using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Infrastructure.Services;

public class PrefilterService : IPrefilterService
{
    private static readonly HashSet<string> StrongKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "PSX", "KSE", "Pakistan Stock Exchange", "dividends", "earnings", "profit",
        "revenue", "acquisition", "merger", "IPO", "rights issue", "bonus shares",
        "SECP", "SBP", "State Bank", "interest rate", "inflation", "GDP",
        "oil prices", "gas prices", "cement", "banking", "textile", "fertilizer",
        "quarterly results", "annual results", "board meeting", "AGM", "EGM"
    };

    private static readonly HashSet<string> WeakKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pakistan", "rupee", "economy", "market", "stocks", "shares",
        "investment", "finance", "business", "trade", "export", "import"
    };

    public bool IsRelevant(NewsArticle article)
    {
        var text = $"{article.Title} {article.Description}";

        var strongHits = StrongKeywords.Count(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        if (strongHits >= 1) return true;

        var weakHits = WeakKeywords.Count(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        return weakHits >= 2;
    }
}
