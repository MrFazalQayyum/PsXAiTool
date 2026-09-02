using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Core.Settings;
using PsXAiTool.Infrastructure.Data;
using PsXAiTool.Infrastructure.Jobs;
using PsXAiTool.Infrastructure.Scrapers;
using PsXAiTool.Infrastructure.Services;

namespace PsXAiTool.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var settings = config.GetSection("App").Get<AppSettings>() ?? new AppSettings();

        // Railway provides DATABASE_URL as a postgresql:// URI; convert it to Npgsql format
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
            settings.ConnectionString = ParseDatabaseUrl(databaseUrl);

        // Allow individual Railway env vars to override specific AppSettings values
        settings.AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? settings.AnthropicApiKey;
        settings.VapidPublicKey  = Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY")  ?? settings.VapidPublicKey;
        settings.VapidPrivateKey = Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY") ?? settings.VapidPrivateKey;

        // Register the resolved settings so IOptions<AppSettings> sees env-var values too
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings));

        // EF Core + PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(settings.ConnectionString));

        // Hangfire
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(settings.ConnectionString));
        services.AddHangfireServer();

        // HTTP clients
        services.AddHttpClient<ClaudeService>();
        services.AddHttpClient<YahooFinanceScraper>(c =>
        {
            c.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });
        services.AddHttpClient<NewsScraperService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        // Services
        services.AddScoped<IMarketService, MarketService>();
        services.AddScoped<ISignalService, SignalService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IClaudeService, ClaudeService>();
        services.AddScoped<INewsScraperService, NewsScraperService>();
        services.AddScoped<IPrefilterService, PrefilterService>();

        // Scrapers / jobs
        services.AddScoped<YahooFinanceScraper>();
        services.AddScoped<BackgroundJobs>();
        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<CompanySeed>();

        return services;
    }

    /// <summary>
    /// Converts a Railway postgresql:// URI to an Npgsql connection string.
    /// Input:  postgresql://user:pass@host:5432/dbname
    /// Output: Host=host;Port=5432;Database=dbname;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
    /// </summary>
    private static string ParseDatabaseUrl(string url)
    {
        // Strip scheme
        url = url.Replace("postgresql://", "").Replace("postgres://", "");

        // user:pass@host:port/db
        var atIdx      = url.IndexOf('@');
        var userInfo   = url[..atIdx];
        var hostPart   = url[(atIdx + 1)..];

        var colonInUser = userInfo.IndexOf(':');
        var user     = Uri.UnescapeDataString(userInfo[..colonInUser]);
        var pass     = Uri.UnescapeDataString(userInfo[(colonInUser + 1)..]);

        var slashIdx = hostPart.IndexOf('/');
        var hostPort = hostPart[..slashIdx];
        var db       = hostPart[(slashIdx + 1)..];

        var colonInHost = hostPort.LastIndexOf(':');
        var host = hostPort[..colonInHost];
        var portStr = hostPort[(colonInHost + 1)..];

        // Railway PostgreSQL requires SSL
        return $"Host={host};Port={portStr};Database={db};Username={user};Password={pass};" +
               "SSL Mode=Require;Trust Server Certificate=true";
    }
}
