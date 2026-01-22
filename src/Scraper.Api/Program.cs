using Microsoft.Extensions.Caching.Memory;
using Scraper.Api.Models;
using Scraper.Api.Services;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Core.Normalizers;
using Scraper.Infrastructure.Scrapers;
using Scraper.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // Limit cache entries
});

builder.Services.AddHttpClient();

// Register core services
builder.Services.AddSingleton<ITitleNormalizer, TitleNormalizer>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IFlareSolverrService, FlareSolverrService>();

// Register ScraperService with IConfigurationService dependency
builder.Services.AddSingleton<IScraperService>(sp =>
{
    var scrapers = sp.GetServices<IScraper>();
    var logger = sp.GetRequiredService<ILogger<ScraperService>>();
    var configService = sp.GetRequiredService<IConfigurationService>();
    return new ScraperService(scrapers, logger, configService);
});

// Register scrapers
// Add your scrapers here
builder.Services.AddSingleton<IScraper>(sp =>
{
    var titleNormalizer = sp.GetRequiredService<ITitleNormalizer>();
    var logger = sp.GetRequiredService<ILogger<ApacheTorrentScraper>>();
    var flareSolverrService = sp.GetService<IFlareSolverrService>();
    return new ApacheTorrentScraper(titleNormalizer, logger, flareSolverrService);
});
// builder.Services.AddSingleton<IScraper, ExampleScraper>(); // Disabled - using ApacheTorrentScraper instead

// Register API services
builder.Services.AddSingleton<TorznabService>();

// Enable static files
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Enable static files and default files
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure caching from configuration
var cache = app.Services.GetRequiredService<IMemoryCache>();
var configServiceForCache = app.Services.GetRequiredService<IConfigurationService>();
var appConfig = configServiceForCache.GetConfigurationAsync().GetAwaiter().GetResult();

var cacheOptions = new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(appConfig.CacheExpirationMinutes > 0 ? appConfig.CacheExpirationMinutes : 10),
    SlidingExpiration = TimeSpan.FromMinutes(appConfig.CacheSlidingExpirationMinutes > 0 ? appConfig.CacheSlidingExpirationMinutes : 5),
    Size = 1
};

// Torznab API endpoint
app.MapGet("/api", async (HttpContext context, TorznabService torznabService, IConfigurationService configService) =>
{
    var queryParams = context.Request.Query;
    var t = queryParams["t"].ToString().ToLowerInvariant();
    var q = queryParams["q"].ToString();
    var imdbId = queryParams["imdbid"].ToString();
    var season = queryParams["season"].ToString();
    var episode = queryParams["episode"].ToString();
    var apiKey = queryParams["apikey"].ToString();
    var cat = queryParams["cat"].ToString(); // Categories (e.g., "2000,2010,2020")
    var extended = queryParams["extended"].ToString(); // Extended attributes

    // Validate API key if configured
    var config = await configService.GetConfigurationAsync();
    if (!string.IsNullOrWhiteSpace(config.ApiKey))
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey != config.ApiKey)
        {
            Log.Warning("Invalid or missing API key");
            return Results.Unauthorized();
        }
    }

    // Handle caps endpoint (capabilities)
    if (t == "caps")
    {
        return Results.Content(GetCapsXml(), "application/xml");
    }

    // Handle validation/test requests without query
    // Prowlarr/Radarr/Sonarr send requests without 'q' or 'imdbid' to validate the indexer
    if (string.IsNullOrEmpty(q) && string.IsNullOrEmpty(imdbId))
    {
        Log.Information("Validation request received (no query) - returning empty RSS feed");
        // Return empty but valid RSS feed for validation
        var emptyRss = new TorznabRss
        {
            Channel = new TorznabChannel
            {
                Title = "Media Scraper",
                Description = "Torznab-compatible media scraper",
                Link = string.Empty,
                Language = "en-us",
                Items = new List<TorznabItem>()
            }
        };
        var xml = torznabService.SerializeToXml(emptyRss);
        return Results.Content(xml, "application/xml");
    }

    // Determine media type
    MediaType? mediaType = null;
    if (t == "movie" || !string.IsNullOrEmpty(imdbId))
    {
        mediaType = MediaType.Movie;
    }
    else if (t == "tvsearch")
    {
        mediaType = MediaType.TvShow;
    }

    // Parse categories if provided (filter results by category)
    var categories = new List<int>();
    if (!string.IsNullOrEmpty(cat))
    {
        var catParts = cat.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var catPart in catParts)
        {
            if (int.TryParse(catPart.Trim(), out var catId))
            {
                categories.Add(catId);
            }
        }
    }

    // Build cache key (include categories for proper caching)
    var cacheKey = $"torznab_{t}_{q}_{imdbId}_{season}_{episode}_{cat}";

    // Check cache
    if (cache.TryGetValue(cacheKey, out string? cachedXml))
    {
        Log.Information("Returning cached result for {CacheKey}", cacheKey);
        return Results.Content(cachedXml, "application/xml");
    }

    try
    {
        // Perform search
        var rss = await torznabService.SearchAsync(q, 
            string.IsNullOrEmpty(imdbId) ? null : imdbId, 
            mediaType);

        // Filter by categories if specified
        if (categories.Any())
        {
            rss.Channel.Items = rss.Channel.Items.Where(item =>
            {
                // Category 2000 = Movies, 5000 = TV
                // Categories is a List<string>, so we need to parse them
                foreach (var catStr in item.Categories ?? new List<string>())
                {
                    if (int.TryParse(catStr, out var catId) && categories.Contains(catId))
                    {
                        return true;
                    }
                }
                return false;
            }).ToList();
        }

        // Serialize to XML
        var xml = torznabService.SerializeToXml(rss);

        // Cache the result
        cache.Set(cacheKey, xml, cacheOptions);

        return Results.Content(xml, "application/xml");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error processing Torznab request");
        return Results.Problem("Internal server error", statusCode: 500);
    }
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Configuration API endpoints
app.MapGet("/api/config", async (IConfigurationService configService) =>
{
    try
    {
        var config = await configService.GetConfigurationAsync();
        return Results.Ok(config);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error loading configuration");
        return Results.Problem("Failed to load configuration", statusCode: 500);
    }
});

app.MapPost("/api/config", async (HttpRequest request, IConfigurationService configService) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync();
        
        Log.Debug("Received configuration JSON: {Length} characters", json.Length);
        
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
        };
        
        var config = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json, options);
        
        if (config == null)
        {
            Log.Warning("Failed to deserialize configuration - result is null");
            return Results.BadRequest("Invalid configuration data: deserialization returned null");
        }
        
        // Ensure API key is preserved if it exists in current config
        var currentConfig = await configService.GetConfigurationAsync();
        if (string.IsNullOrWhiteSpace(config.ApiKey) && !string.IsNullOrWhiteSpace(currentConfig.ApiKey))
        {
            config.ApiKey = currentConfig.ApiKey;
        }
        
        Log.Information("Saving configuration with {ScraperCount} scrapers", config.Scrapers?.Count ?? 0);
        await configService.SaveConfigurationAsync(config);
        
        return Results.Ok(new { message = "Configuration saved successfully" });
    }
    catch (System.Text.Json.JsonException ex)
    {
        Log.Error(ex, "JSON deserialization error: {Message}", ex.Message);
        return Results.BadRequest($"Invalid JSON format: {ex.Message}");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error saving configuration: {Message}", ex.Message);
        return Results.Problem($"Failed to save configuration: {ex.Message}", statusCode: 500);
    }
});

app.MapGet("/api/generate-apikey", () =>
{
    // Generate a new API key
    const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
    var random = new Random();
    var apiKey = new string(Enumerable.Repeat(chars, 32)
        .Select(s => s[random.Next(s.Length)]).ToArray());
    
    return Results.Ok(new { apiKey });
});

app.MapGet("/api/scrapers", () =>
{
    try
    {
        var scrapers = app.Services.GetServices<IScraper>()
            .Select(s => new
            {
                name = s.Name,
                isEnabled = s.IsEnabled
            })
            .ToList();

        return Results.Ok(scrapers);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error listing scrapers");
        return Results.Problem("Failed to list scrapers", statusCode: 500);
    }
});

app.MapGet("/api/test", async () =>
{
    try
    {
        var scraperService = app.Services.GetRequiredService<IScraperService>();
        var testRequest = new SearchRequest { Query = "test" };
        var results = await scraperService.SearchAsync(testRequest);
        
        return Results.Ok(new
        {
            success = true,
            message = $"Test successful. Found {results.Count()} results.",
            resultsCount = results.Count()
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Connection test failed");
        return Results.Ok(new
        {
            success = false,
            message = ex.Message
        });
    }
});

// Root redirect to UI
app.MapGet("/", () => Results.Redirect("/index.html"));

// Configure URL
app.Urls.Add("http://0.0.0.0:9898");

app.Run();

static string GetCapsXml()
{
    return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<caps>
    <server version=""1.0"" title=""Media Scraper"" />
    <limits max=""100"" default=""50"" />
    <retention days=""365"" />
    <searching>
        <search available=""yes"" supportedParams=""q"" />
        <tv-search available=""yes"" supportedParams=""q,season,ep"" />
        <movie-search available=""yes"" supportedParams=""q,imdbid"" />
    </searching>
    <categories>
        <category id=""2000"" name=""Movies"" />
        <category id=""5000"" name=""TV"" />
    </categories>
</caps>";
}

