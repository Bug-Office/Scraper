using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;

namespace Scraper.Infrastructure.Services;

/// <summary>
/// Service for managing scraper configurations
/// Converts between ScraperConfig (database) and ScraperConfiguration (internal use)
/// </summary>
public class ScraperConfigurationService
{
    private readonly ScraperConfigService _scraperConfigService;
    private readonly ILogger<ScraperConfigurationService> _logger;
    private readonly string? _defaultScrapersJsonPath;
    private Dictionary<string, ScraperConfiguration>? _defaultConfigurationsCache;

    public ScraperConfigurationService(
        ScraperConfigService scraperConfigService,
        ILogger<ScraperConfigurationService> logger,
        string? defaultScrapersJsonPath = null)
    {
        _scraperConfigService = scraperConfigService;
        _logger = logger;
        _defaultScrapersJsonPath = defaultScrapersJsonPath;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Gets scraper configuration from database or returns default
    /// </summary>
    public async Task<ScraperConfiguration> GetScraperConfigurationAsync(string scraperName)
    {
        try
        {
            var scraperConfig = await _scraperConfigService.GetScraperConfigAsync(scraperName);
            
            if (scraperConfig != null && scraperConfig.Settings.Any())
            {
                try
                {
                    var config = DeserializeFromSettings(scraperConfig.Settings, scraperName);
                    // Validate that we got meaningful data
                    if (!string.IsNullOrEmpty(config.BaseUrl))
                    {
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize configuration for {ScraperName}, using defaults", scraperName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading configuration for {ScraperName}, using defaults", scraperName);
        }

        // Return default configuration
        return GetDefaultConfiguration(scraperName);
    }

    /// <summary>
    /// Gets scraper configuration synchronously (for use in constructors)
    /// Uses a simple synchronous approach with timeout
    /// </summary>
    public ScraperConfiguration GetScraperConfiguration(string scraperName)
    {
        try
        {
            // Use Task.Run to avoid deadlock issues
            var task = Task.Run(async () => await GetScraperConfigurationAsync(scraperName));
            if (task.Wait(TimeSpan.FromSeconds(2)))
            {
                return task.Result;
            }
            _logger.LogWarning("Timeout loading configuration for {ScraperName}, using defaults", scraperName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading configuration for {ScraperName}, using defaults", scraperName);
        }

        return GetDefaultConfiguration(scraperName);
    }

    /// <summary>
    /// Saves scraper configuration to database
    /// </summary>
    public async Task SaveScraperConfigurationAsync(string scraperName, ScraperConfiguration configuration)
    {
        try
        {
            var settings = SerializeToSettings(configuration);
            
            _logger.LogInformation("Saving scraper configuration for {ScraperName} with {SettingsCount} settings", scraperName, settings.Count);
            
            var scraperConfig = await _scraperConfigService.GetScraperConfigAsync(scraperName);
            if (scraperConfig == null)
            {
                _logger.LogInformation("Creating new scraper configuration for {ScraperName}", scraperName);
                scraperConfig = new ScraperConfig
                {
                    Name = scraperName,
                    IsEnabled = true,
                    Settings = settings
                };
            }
            else
            {
                _logger.LogInformation("Updating existing scraper configuration for {ScraperName}", scraperName);
                scraperConfig.Settings = settings;
            }

            await _scraperConfigService.SaveScraperConfigAsync(scraperConfig);
            _logger.LogInformation("Successfully saved scraper configuration for {ScraperName}", scraperName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving scraper configuration for {ScraperName}", scraperName);
            throw;
        }
    }

    private ScraperConfiguration DeserializeFromSettings(Dictionary<string, string> settings, string scraperName)
    {
        var config = new ScraperConfiguration();

        if (settings.TryGetValue("baseUrl", out var baseUrl))
            config.BaseUrl = baseUrl;

        if (settings.TryGetValue("searchUrlTemplate", out var searchUrlTemplate))
            config.SearchUrlTemplate = searchUrlTemplate;

        if (settings.TryGetValue("resultItemSelectors", out var resultItemSelectorsJson) && !string.IsNullOrEmpty(resultItemSelectorsJson))
        {
            try
            {
                config.ResultItemSelectors = JsonSerializer.Deserialize<List<string>>(resultItemSelectorsJson, JsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize resultItemSelectors for {ScraperName}", scraperName);
                config.ResultItemSelectors = new List<string>();
            }
        }

        if (settings.TryGetValue("titleLinkSelectors", out var titleLinkSelectorsJson) && !string.IsNullOrEmpty(titleLinkSelectorsJson))
        {
            try
            {
                config.TitleLinkSelectors = JsonSerializer.Deserialize<List<string>>(titleLinkSelectorsJson, JsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize titleLinkSelectors for {ScraperName}", scraperName);
                config.TitleLinkSelectors = new List<string>();
            }
        }

        if (settings.TryGetValue("downloadSectionSelectors", out var downloadSectionSelectorsJson) && !string.IsNullOrEmpty(downloadSectionSelectorsJson))
        {
            try
            {
                config.DownloadSectionSelectors = JsonSerializer.Deserialize<List<string>>(downloadSectionSelectorsJson, JsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize downloadSectionSelectors for {ScraperName}", scraperName);
                config.DownloadSectionSelectors = new List<string>();
            }
        }

        if (settings.TryGetValue("episodeParagraphSelectors", out var episodeParagraphSelectorsJson) && !string.IsNullOrEmpty(episodeParagraphSelectorsJson))
        {
            try
            {
                config.EpisodeParagraphSelectors = JsonSerializer.Deserialize<List<string>>(episodeParagraphSelectorsJson, JsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize episodeParagraphSelectors for {ScraperName}", scraperName);
                config.EpisodeParagraphSelectors = new List<string>();
            }
        }

        if (settings.TryGetValue("infoSectionSelectors", out var infoSectionSelectorsJson) && !string.IsNullOrEmpty(infoSectionSelectorsJson))
        {
            try
            {
                config.InfoSectionSelectors = JsonSerializer.Deserialize<List<string>>(infoSectionSelectorsJson, JsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize infoSectionSelectors for {ScraperName}", scraperName);
                config.InfoSectionSelectors = new List<string>();
            }
        }

        if (settings.TryGetValue("titleCleanupPatterns", out var titleCleanupPatternsJson) && !string.IsNullOrEmpty(titleCleanupPatternsJson))
        {
            try
            {
                config.TitleCleanupPatterns = JsonSerializer.Deserialize<List<string>>(titleCleanupPatternsJson, JsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize titleCleanupPatterns for {ScraperName}", scraperName);
                config.TitleCleanupPatterns = new List<string>();
            }
        }

        return config;
    }

    private Dictionary<string, string> SerializeToSettings(ScraperConfiguration configuration)
    {
        if (configuration == null)
        {
            _logger.LogWarning("Attempted to serialize null configuration");
            return new Dictionary<string, string>();
        }

        var settings = new Dictionary<string, string>
        {
            ["baseUrl"] = configuration.BaseUrl ?? string.Empty,
            ["searchUrlTemplate"] = configuration.SearchUrlTemplate ?? string.Empty,
            ["resultItemSelectors"] = JsonSerializer.Serialize(configuration.ResultItemSelectors ?? new List<string>(), JsonOptions),
            ["titleLinkSelectors"] = JsonSerializer.Serialize(configuration.TitleLinkSelectors ?? new List<string>(), JsonOptions),
            ["downloadSectionSelectors"] = JsonSerializer.Serialize(configuration.DownloadSectionSelectors ?? new List<string>(), JsonOptions),
            ["episodeParagraphSelectors"] = JsonSerializer.Serialize(configuration.EpisodeParagraphSelectors ?? new List<string>(), JsonOptions),
            ["infoSectionSelectors"] = JsonSerializer.Serialize(configuration.InfoSectionSelectors ?? new List<string>(), JsonOptions),
            ["titleCleanupPatterns"] = JsonSerializer.Serialize(configuration.TitleCleanupPatterns ?? new List<string>(), JsonOptions)
        };

        _logger.LogDebug("Serialized configuration: BaseUrl={BaseUrl}, SearchUrlTemplate={SearchUrlTemplate}, ResultItemSelectors={ResultItemSelectorsCount}, TitleLinkSelectors={TitleLinkSelectorsCount}",
            settings["baseUrl"], settings["searchUrlTemplate"],
            configuration.ResultItemSelectors?.Count ?? 0,
            configuration.TitleLinkSelectors?.Count ?? 0);

        return settings;
    }

    private ScraperConfiguration GetDefaultConfiguration(string scraperName)
    {
        // Try to load from JSON file first
        var configFromJson = LoadDefaultFromJson(scraperName);
        if (configFromJson != null)
        {
            return configFromJson;
        }

        // Fallback to generic default
        return new ScraperConfiguration
        {
            BaseUrl = string.Empty,
            SearchUrlTemplate = "{BaseUrl}/search?q={Query}",
            ResultItemSelectors = new List<string> { "//article", "//div[@class='item']" },
            TitleLinkSelectors = new List<string> { ".//h2//a", ".//a" },
            DownloadSectionSelectors = new List<string> { "//div[@id='download']" },
            EpisodeParagraphSelectors = new List<string> { ".//p[contains(., 'EPISÓDIO')]" },
            InfoSectionSelectors = new List<string> { "//div[contains(@class, 'info')]" },
            TitleCleanupPatterns = new List<string>()
        };
    }

    private ScraperConfiguration? LoadDefaultFromJson(string scraperName)
    {
        try
        {
            // Build cache if not exists
            if (_defaultConfigurationsCache == null)
            {
                _defaultConfigurationsCache = LoadDefaultConfigurationsFromJson();
            }

            // Try to get from cache
            if (_defaultConfigurationsCache.TryGetValue(scraperName, out var config))
            {
                return config;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading default configuration from JSON for {ScraperName}", scraperName);
        }

        return null;
    }

    private Dictionary<string, ScraperConfiguration> LoadDefaultConfigurationsFromJson()
    {
        var cache = new Dictionary<string, ScraperConfiguration>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Try to find the default-scrapers.json file
            var jsonPath = _defaultScrapersJsonPath;
            if (string.IsNullOrEmpty(jsonPath))
            {
                // Try common locations
                var currentDir = Directory.GetCurrentDirectory();
                var possiblePaths = new[]
                {
                    Path.Combine(currentDir, "data", "default-scrapers.json"),
                    Path.Combine(currentDir, "src", "Scraper.Api", "data", "default-scrapers.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "default-scrapers.json")
                };

                jsonPath = possiblePaths.FirstOrDefault(File.Exists);
            }

            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                _logger.LogDebug("Default scrapers JSON file not found, using generic defaults");
                return cache;
            }

            var jsonContent = File.ReadAllText(jsonPath);
            var defaults = JsonSerializer.Deserialize<DefaultScrapersFile>(jsonContent, JsonOptions);

            if (defaults?.Scrapers != null)
            {
                foreach (var scraper in defaults.Scrapers)
                {
                    var config = ConvertToScraperConfiguration(scraper.Configuration);
                    cache[scraper.Name] = config;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading default configurations from JSON file");
        }

        return cache;
    }

    private ScraperConfiguration ConvertToScraperConfiguration(DefaultScraperConfiguration config)
    {
        return new ScraperConfiguration
        {
            BaseUrl = config.BaseUrl ?? string.Empty,
            SearchUrlTemplate = config.SearchUrlTemplate ?? "{BaseUrl}/search?q={Query}",
            ResultItemSelectors = config.ResultItemSelectors ?? new List<string>(),
            TitleLinkSelectors = config.TitleLinkSelectors ?? new List<string>(),
            DownloadSectionSelectors = config.DownloadSectionSelectors ?? new List<string>(),
            EpisodeParagraphSelectors = config.EpisodeParagraphSelectors ?? new List<string>(),
            InfoSectionSelectors = config.InfoSectionSelectors ?? new List<string>(),
            TitleCleanupPatterns = config.TitleCleanupPatterns ?? new List<string>()
        };
    }

    private class DefaultScrapersFile
    {
        public List<DefaultScraper>? Scrapers { get; set; }
    }

    private class DefaultScraper
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DefaultScraperConfiguration Configuration { get; set; } = new();
    }

    private class DefaultScraperConfiguration
    {
        public string? BaseUrl { get; set; }
        public string? SearchUrlTemplate { get; set; }
        public List<string>? ResultItemSelectors { get; set; }
        public List<string>? TitleLinkSelectors { get; set; }
        public List<string>? DownloadSectionSelectors { get; set; }
        public List<string>? EpisodeParagraphSelectors { get; set; }
        public List<string>? InfoSectionSelectors { get; set; }
        public List<string>? TitleCleanupPatterns { get; set; }
    }
}
