using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PsXAiTool.Core.Entities;
using PsXAiTool.Core.Enums;

namespace PsXAiTool.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<DailyPrice> DailyPrices => Set<DailyPrice>();
    public DbSet<MarketIndex> MarketIndices => Set<MarketIndex>();
    public DbSet<Portfolio> Portfolio => Set<Portfolio>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<Signal> Signals => Set<Signal>();
    public DbSet<SignalValidation> SignalValidations => Set<SignalValidation>();
    public DbSet<DailyBriefing> DailyBriefings => Set<DailyBriefing>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyPrice>(e =>
        {
            e.HasIndex(p => new { p.Date, p.Symbol }).IsUnique();
            e.HasOne(p => p.Company)
             .WithMany(c => c.DailyPrices)
             .HasForeignKey(p => p.Symbol)
             .HasPrincipalKey(c => c.Symbol)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MarketIndex>(e =>
        {
            e.HasIndex(i => new { i.Date, i.IndexName }).IsUnique();
        });

        modelBuilder.Entity<Portfolio>(e =>
        {
            e.HasIndex(p => p.Symbol).IsUnique();
            e.HasOne(p => p.Company)
             .WithMany(c => c.PortfolioHoldings)
             .HasForeignKey(p => p.Symbol)
             .HasPrincipalKey(c => c.Symbol)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NewsArticle>(e =>
        {
            e.HasIndex(a => a.Url).IsUnique();
        });

        // Use explicit value converters instead of native jsonb to avoid
        // Npgsql EmptyProjectionMember bug with List<string> JSON columns
        var listConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var listComparer = new ValueComparer<List<string>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<Signal>(e =>
        {
            e.Property(s => s.Entities).HasConversion(listConverter, listComparer).HasColumnType("text");
            e.Property(s => s.Sectors).HasConversion(listConverter, listComparer).HasColumnType("text");
            e.Property(s => s.Direction)
             .HasConversion(d => d.ToString(), s => Enum.Parse<SignalDirection>(s));
        });

        modelBuilder.Entity<SignalValidation>(e =>
        {
            e.HasIndex(v => v.SignalId).IsUnique();
            e.HasOne(v => v.Signal)
             .WithOne(s => s.Validation)
             .HasForeignKey<SignalValidation>(v => v.SignalId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(v => v.Verdict)
             .HasConversion(v => v.ToString(), s => Enum.Parse<ValidationVerdict>(s));
        });

        modelBuilder.Entity<PushSubscription>(e =>
        {
            e.HasIndex(p => p.Endpoint).IsUnique();
        });
    }
}
