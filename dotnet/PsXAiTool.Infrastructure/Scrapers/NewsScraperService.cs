using System.ServiceModel.Syndication;
using System.Xml;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Infrastructure.Scrapers;

public class NewsScraperService(HttpClient httpClient, ILogger<NewsScraperService> logger) : INewsScraperService
{
    private static readonly string[] RssFeeds =
    [
        "https://www.brecorder.com/feed",
        "https://propakistani.pk/feed",
        "https://arynews.tv/feed"
    ];

    public async Task<List<NewsArticle>> ScrapeRssFeedsAsync()
    {
        var articles = new List<NewsArticle>();

        foreach (var feedUrl in RssFeeds)
        {
            try
            {
                var source = new Uri(feedUrl).Host.Replace("www.", "");
                var xml = await httpClient.GetStringAsync(feedUrl);
                using var reader = XmlReader.Create(new StringReader(xml));
                var feed = SyndicationFeed.Load(reader);

                foreach (var item in feed.Items)
                {
                    var url = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(url)) continue;

                    articles.Add(new NewsArticle
                    {
                        Source = source,
                        Url = url,
                        Title = item.Title?.Text ?? string.Empty,
                        Description = item.Summary?.Text,
                        PublishedAt = item.PublishDate.UtcDateTime == default
                            ? DateTime.UtcNow
                            : item.PublishDate.UtcDateTime
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("RSS scrape failed for {Url}: {Error}", feedUrl, ex.Message);
            }
        }

        return articles;
    }

    public async Task<List<NewsArticle>> ScrapePsxAnnouncementsAsync()
    {
        var articles = new List<NewsArticle>();

        try
        {
            // PSX announcements via form POST (simulates the dps.psx.com.pk search)
            var formData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["company"] = "",
                ["noticetype"] = "",
                ["startdate"] = DateTime.UtcNow.AddDays(-1).ToString("MM/dd/yyyy"),
                ["enddate"] = DateTime.UtcNow.ToString("MM/dd/yyyy")
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "https://dps.psx.com.pk/announcements")
            {
                Content = formData
            };
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Referer", "https://dps.psx.com.pk/");

            var response = await httpClient.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var rows = doc.DocumentNode.SelectNodes("//table//tr[td]");
            if (rows is null) return articles;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells is null || cells.Count < 4) continue;

                var symbol = cells[0].InnerText.Trim();
                var subject = cells[2].InnerText.Trim();
                var link = cells[3].SelectSingleNode(".//a");
                var url = link?.GetAttributeValue("href", "") ?? string.Empty;

                if (string.IsNullOrEmpty(subject)) continue;

                articles.Add(new NewsArticle
                {
                    Source = "psx",
                    Url = url.StartsWith("http") ? url : $"https://dps.psx.com.pk{url}",
                    Title = $"{symbol}: {subject}",
                    PublishedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("PSX announcements scrape failed: {Error}", ex.Message);
        }

        return articles;
    }
}
