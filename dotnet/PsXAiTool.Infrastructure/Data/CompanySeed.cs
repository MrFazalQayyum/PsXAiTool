using CsvHelper;
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

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
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
        public string Symbol { get; set; } = string.Empty;
        public string YahooTicker { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public decimal? MarketCap { get; set; }
        public long? SharesOutstanding { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
