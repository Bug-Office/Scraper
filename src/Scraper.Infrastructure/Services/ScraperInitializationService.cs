using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;

namespace Scraper.Infrastructure.Services;

/// <summary>
/// Service for initializing default scraper configurations from JSON file
/// </summary>
public class ScraperInitializationService
{
    private readonly ScraperConfigService _scraperConfigService;
    private readonly ScraperConfigurationService _scraperConfigurationService;
    private readonly ILogger<ScraperInitializationService> _logger;

    public ScraperInitializationService(
        ScraperConfigService scraperConfigService,
        ScraperConfigurationService scraperConfigurationService,
        ILogger<ScraperInitializationService> logger)
    {
        _scraperConfigService = scraperConfigService;
        _scraperConfigurationService = scraperConfigurationService;
        _logger = logger;
    }

    /// <summary>
    /// Initializes default scrapers from JSON file if they don't exist in database
    /// </summary>
    public async Task InitializeDefaultsAsync(string defaultScrapersJsonPath)
    {
        try
        {
            if (!File.Exists(defaultScrapersJsonPath))
            {
                _logger.LogWarning("Default scrapers file not found at {Path}", defaultScrapersJsonPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(defaultScrapersJsonPath);
            var defaults = JsonSerializer.Deserialize<DefaultScrapersFile>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (defaults?.Scrapers == null)
            {
                _logger.LogWarning("Invalid default scrapers file format");
                return;
            }

            var existingScrapers = await _scraperConfigService.GetAllScraperConfigsAsync();
            var existingScraperNames = existingScrapers.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var defaultScraper in defaults.Scrapers)
            {
                if (existingScraperNames.Contains(defaultScraper.Name))
                {
                    _logger.LogDebug("Scraper {ScraperName} already exists, skipping", defaultScraper.Name);
                    continue;
                }

                try
                {
                    var scraperConfig = ConvertToScraperConfiguration(defaultScraper.Configuration);
                    await _scraperConfigurationService.SaveScraperConfigurationAsync(defaultScraper.Name, scraperConfig);

                    // Set IsEnabled status
                    var dbConfig = await _scraperConfigService.GetScraperConfigAsync(defaultScraper.Name);
                    if (dbConfig != null)
                    {
                        dbConfig.IsEnabled = defaultScraper.IsEnabled;
                        await _scraperConfigService.SaveScraperConfigAsync(dbConfig);
                    }

                    _logger.LogInformation("Initialized default scraper {ScraperName}", defaultScraper.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing scraper {ScraperName}", defaultScraper.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default scrapers");
        }
    }

    /// <summary>
    /// Gets the default template for creating new scrapers
    /// </summary>
    public async Task<DefaultScraperTemplate?> GetDefaultTemplateAsync(string defaultScrapersJsonPath)
    {
        try
        {
            if (!File.Exists(defaultScrapersJsonPath))
            {
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(defaultScrapersJsonPath);
            var defaults = JsonSerializer.Deserialize<DefaultScrapersFile>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return defaults?.DefaultTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading default template");
            return null;
        }
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
        public DefaultScraperTemplate? DefaultTemplate { get; set; }
    }

    private class DefaultScraper
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DefaultScraperConfiguration Configuration { get; set; } = new();
    }

    public class DefaultScraperTemplate
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DefaultScraperConfiguration Configuration { get; set; } = new();
    }

    public class DefaultScraperConfiguration
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
