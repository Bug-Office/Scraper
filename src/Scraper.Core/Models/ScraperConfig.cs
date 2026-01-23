namespace Scraper.Core.Models;

public class ScraperConfig
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, string> Settings { get; set; } = new();
}

public class AppConfiguration
{
    public List<ScraperConfig> Scrapers { get; set; } = new();
    public int CacheExpirationMinutes { get; set; } = 10;
    public int CacheSlidingExpirationMinutes { get; set; } = 5;
    public int CacheMaxResultsPerIndexer { get; set; } = 1000;
    public string? FlareSolverrUrl { get; set; }
    public int FlareSolverrMaxTimeoutMs { get; set; } = 240000; // 4 minutes default
    public string? ApiKey { get; set; }
    public int ServerPort { get; set; } = 9898;
    public string? BaseUrlOverride { get; set; }
    public bool AllowCors { get; set; } = false;
    public bool EnhancedLogging { get; set; } = false;
    public string? TmdbApiKey { get; set; }
}

