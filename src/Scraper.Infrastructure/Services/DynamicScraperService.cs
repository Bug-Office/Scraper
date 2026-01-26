using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;
using Scraper.Infrastructure.Interfaces;
using Scraper.Infrastructure.Scrapers;

namespace Scraper.Infrastructure.Services;

/// <summary>
/// Service for managing dynamic scrapers created from database configurations
/// </summary>
public class DynamicScraperService
{
    private readonly ScraperConfigurationService _configService;
    private readonly ScraperConfigService _scraperConfigService;
    private readonly ITitleNormalizer _titleNormalizer;
    private readonly ITmdbService _tmdbService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DynamicScraperService> _logger;
    private readonly IFlareSolverrService? _flareSolverrService;
    private readonly IMediaItemRepository? _mediaItemRepository;
    
    private readonly Dictionary<string, IScraper> _scraperCache = new();

    public DynamicScraperService(
        ScraperConfigurationService configService,
        ScraperConfigService scraperConfigService,
        ITitleNormalizer titleNormalizer,
        ITmdbService tmdbService,
        ILoggerFactory loggerFactory,
        ILogger<DynamicScraperService> logger,
        IFlareSolverrService? flareSolverrService = null,
        IMediaItemRepository? mediaItemRepository = null)
    {
        _configService = configService;
        _scraperConfigService = scraperConfigService;
        _titleNormalizer = titleNormalizer;
        _tmdbService = tmdbService;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _flareSolverrService = flareSolverrService;
        _mediaItemRepository = mediaItemRepository;
    }

    /// <summary>
    /// Gets all scrapers from database/JSON configuration
    /// </summary>
    public async Task<IEnumerable<IScraper>> GetAllScrapersAsync(IEnumerable<IScraper>? staticScrapers = null)
    {
        var allScrapers = new List<IScraper>();
        
        try
        {
            var scraperConfigs = await _scraperConfigService.GetAllScraperConfigsAsync();
            var scraperNames = scraperConfigs.Select(s => s.Name).ToList();

            foreach (var scraperName in scraperNames)
            {
                try
                {
                    var scraper = await GetOrCreateScraperAsync(scraperName);
                    if (scraper != null)
                    {
                        allScrapers.Add(scraper);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load scraper {ScraperName}", scraperName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scrapers");
        }

        return allScrapers;
    }

    /// <summary>
    /// Gets or creates a scraper instance for the given name
    /// </summary>
    public async Task<IScraper?> GetOrCreateScraperAsync(string scraperName)
    {
        // Check cache first
        if (_scraperCache.TryGetValue(scraperName, out var cachedScraper))
        {
            return cachedScraper;
        }

        try
        {
            var configuration = await _configService.GetScraperConfigurationAsync(scraperName);
            
            // Validate configuration
            if (string.IsNullOrEmpty(configuration.BaseUrl))
            {
                _logger.LogWarning("Scraper {ScraperName} has no BaseUrl configured", scraperName);
                return null;
            }

            var logger = _loggerFactory.CreateLogger<ConfigurableTorrentScraper>();
            var scraper = new ConfigurableTorrentScraper(
                scraperName,
                configuration,
                _titleNormalizer,
                logger,
                _tmdbService,
                _loggerFactory,
                _flareSolverrService,
                _mediaItemRepository);

            _scraperCache[scraperName] = scraper;
            return scraper;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating scraper {ScraperName}", scraperName);
            return null;
        }
    }

    /// <summary>
    /// Creates a new scraper configuration in the database
    /// </summary>
    public async Task<bool> CreateScraperAsync(string scraperName, ScraperConfiguration configuration, bool isEnabled = true)
    {
        try
        {
            // SaveScraperConfigurationAsync already saves to ScraperConfigs table
            await _configService.SaveScraperConfigurationAsync(scraperName, configuration);
            
            // Update IsEnabled status
            var scraperConfig = await _scraperConfigService.GetScraperConfigAsync(scraperName);
            if (scraperConfig != null)
            {
                scraperConfig.IsEnabled = isEnabled;
                await _scraperConfigService.SaveScraperConfigAsync(scraperConfig);
            }

            // Clear cache to force reload
            _scraperCache.Remove(scraperName);
            
            _logger.LogInformation("Created scraper {ScraperName}", scraperName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating scraper {ScraperName}", scraperName);
            return false;
        }
    }

    /// <summary>
    /// Deletes a scraper configuration from the database
    /// </summary>
    public async Task<bool> DeleteScraperAsync(string scraperName)
    {
        try
        {
            var success = await _scraperConfigService.DeleteScraperConfigAsync(scraperName);
            
            if (success)
            {
                // Clear cache
                _scraperCache.Remove(scraperName);
                _logger.LogInformation("Deleted scraper {ScraperName}", scraperName);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scraper {ScraperName}", scraperName);
            return false;
        }
    }

    /// <summary>
    /// Clears the scraper cache (useful after configuration changes)
    /// </summary>
    public void ClearCache()
    {
        _scraperCache.Clear();
    }
}
