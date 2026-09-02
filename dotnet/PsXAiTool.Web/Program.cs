using Hangfire;
using PsXAiTool.Infrastructure;
using PsXAiTool.Infrastructure.Data;
using PsXAiTool.Infrastructure.Jobs;
using PsXAiTool.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor + Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API Controllers
builder.Services.AddControllers();

// CORS (allow frontend if served separately)
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Infrastructure (EF Core, Hangfire, services, scrapers)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Migrate database and seed on startup
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<CompanySeed>();
    var csvPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "data", "psx_companies.csv");
    await seeder.SeedAsync(csvPath);
}

// Schedule recurring Hangfire jobs
var hangfireJobs = app.Services.GetRequiredService<IRecurringJobManager>();
hangfireJobs.AddOrUpdate<BackgroundJobs>("fetch-prices", j => j.FetchPricesJob(), Cron.Hourly);
hangfireJobs.AddOrUpdate<BackgroundJobs>("process-news", j => j.ProcessNewsJob(), "*/30 * * * *");
hangfireJobs.AddOrUpdate<BackgroundJobs>("daily-briefing", j => j.GenerateBriefingJob(), "0 8 * * *");
hangfireJobs.AddOrUpdate<BackgroundJobs>("validate-signals", j => j.ValidateSignalsJob(), "0 18 * * *");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseAntiforgery();

// Hangfire Dashboard (dev only — add auth middleware for production)
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire");

app.UseRouting();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
