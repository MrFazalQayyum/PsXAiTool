using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PsXAiTool.Infrastructure.Data;

public class DatabaseMigrator(AppDbContext db, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync()
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();

        await CreateTimescaleHypertablesAsync();

        logger.LogInformation("Database migration complete.");
    }

    private async Task CreateTimescaleHypertablesAsync()
    {
        // Attempt TimescaleDB hypertable creation — falls back gracefully if extension is absent.
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb') THEN
                        PERFORM create_hypertable('daily_prices', 'date', if_not_exists => TRUE);
                        PERFORM create_hypertable('market_indices', 'date', if_not_exists => TRUE);
                    END IF;
                END
                $$;");
        }
        catch (Exception ex)
        {
            logger.LogWarning("TimescaleDB hypertable setup skipped: {Message}", ex.Message);
        }
    }
}
