using CsvHelper;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PsXAiTool.Core.Entities;
using System.Globalization;

namespace PsXAiTool.Infrastructure.Data;

public class CompanySeed(AppDbContext db, ILogger<CompanySeed> logger)
{
    public async Task SeedAsync(string csvPath)
    {
        if (await db.Companies.AnyAsync())
        {
            logger.LogInformation("Companies already seeded, skipping.");
            return;
        }

        if (!File.Exists(csvPath))
        {
            logger.LogWarning("Companies CSV not found at {Path}", csvPath);
            return;
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,   // don't throw on unmatched headers
            MissingFieldFound = null  // don't throw on missing columns
        };
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);

        var companies = new List<Company>();
        await foreach (var record in csv.GetRecordsAsync<CsvCompanyRecord>())
        {
            companies.Add(new Company
            {
                Symbol = record.Symbol,
                YahooTicker = record.YahooTicker,
                Name = record.Name,
                Sector = record.Sector,
                MarketCap = record.MarketCap,
                SharesOutstanding = record.SharesOutstanding,
                IsActive = record.IsActive
            });
        }

        db.Companies.AddRange(companies);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} companies.", companies.Count);
    }

    private class CsvCompanyRecord
    {
        [Name("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [Name("yahoo_ticker")]
        public string YahooTicker { get; set; } = string.Empty;

        [Name("name")]
        public string Name { get; set; } = string.Empty;

        [Name("sector")]
        public string Sector { get; set; } = string.Empty;

        [Name("market_cap")]
        public decimal? MarketCap { get; set; }

        [Name("shares_outstanding")]
        public long? SharesOutstanding { get; set; }

        [Name("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
