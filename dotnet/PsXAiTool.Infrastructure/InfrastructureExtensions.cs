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
        services.Configure<AppSettings>(config.GetSection("App"));

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
}
