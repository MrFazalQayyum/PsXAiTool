using PsXAiTool.Core.Entities;

namespace PsXAiTool.Core.Interfaces;

public interface INewsScraperService
{
    Task<List<NewsArticle>> ScrapeRssFeedsAsync();
    Task<List<NewsArticle>> ScrapePsxAnnouncementsAsync();
}
