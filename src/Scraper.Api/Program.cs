using Microsoft.EntityFrameworkCore;
using Scraper.Api.Extensions;
using Scraper.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Configure HTTP client
builder.Services.AddHttpClient();

// Configure database path
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "scraper.db");

// Register all scraper services
builder.Services.AddScraperServices(dbPath);

// Add MVC services
builder.Services.AddControllers();

// Enable static files
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ScraperDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        Log.Information("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error initializing database");
    }
}

// Enable static files and default files
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure routing
app.UseRouting();

// Map controllers
app.MapControllers();

// Configure URL
app.Urls.Add("http://0.0.0.0:9898");

app.Run();

