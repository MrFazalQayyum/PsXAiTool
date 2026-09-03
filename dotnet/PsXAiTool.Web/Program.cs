using Hangfire;
using PsXAiTool.Infrastructure;
using PsXAiTool.Infrastructure.Data;
using PsXAiTool.Infrastructure.Jobs;
using PsXAiTool.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT; honour it so the process listens on the right port
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

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

// Health-check endpoint — Railway uses this to confirm the container is alive
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Migrate database and seed on startup
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<CompanySeed>();

    // In Docker the CSV is copied to /app/data/; locally it lives two dirs up from the web project
    var csvPath = File.Exists("/app/data/psx_companies.csv")
        ? "/app/data/psx_companies.csv"
        : Path.Combine(app.Environment.ContentRootPath, "..", "..", "data", "psx_companies.csv");

    await seeder.SeedAsync(csvPath);
}

// Schedule recurring Hangfire jobs
var hangfireJobs = app.Services.GetRequiredService<IRecurringJobManager>();
hangfireJobs.AddOrUpdate<BackgroundJobs>("fetch-prices",    j => j.FetchPricesJob(),      Cron.Hourly);
hangfireJobs.AddOrUpdate<BackgroundJobs>("process-news",    j => j.ProcessNewsJob(),      "*/30 * * * *");
hangfireJobs.AddOrUpdate<BackgroundJobs>("daily-briefing",  j => j.GenerateBriefingJob(), "0 8 * * *");
hangfireJobs.AddOrUpdate<BackgroundJobs>("validate-signals",j => j.ValidateSignalsJob(),  "0 18 * * *");

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

// Railway terminates TLS at its proxy — do NOT redirect to HTTPS inside the container
app.UseCors();
app.UseStaticFiles();

// Hangfire Dashboard (dev only — add auth middleware for production)
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire");

app.UseRouting();
app.UseAntiforgery();   // must be between UseRouting() and Map*()
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
