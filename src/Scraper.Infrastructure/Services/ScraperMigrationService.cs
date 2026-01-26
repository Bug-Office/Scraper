using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Data;

namespace Scraper.Infrastructure.Services;

/// <summary>
/// Service for migrating scraper data from AppConfiguration to ScraperConfigs table
/// </summary>
public class ScraperMigrationService
{
    private readonly ScraperDbContext _context;
    private readonly IConfigurationService _configService;
    private readonly ScraperConfigService _scraperConfigService;
    private readonly ILogger<ScraperMigrationService> _logger;

    public ScraperMigrationService(
        ScraperDbContext context,
        IConfigurationService configService,
        ScraperConfigService scraperConfigService,
        ILogger<ScraperMigrationService> logger)
    {
        _context = context;
        _configService = configService;
        _scraperConfigService = scraperConfigService;
        _logger = logger;
    }

    /// <summary>
    /// Migrates scrapers from AppConfiguration to ScraperConfigs table
    /// </summary>
    public async Task MigrateScrapersAsync()
    {
        try
        {
            // Check if migration is needed
            var existingScrapers = await _scraperConfigService.GetAllScraperConfigsAsync();
            if (existingScrapers.Any())
            {
                _logger.LogInformation("Scrapers already exist in ScraperConfigs table, skipping migration");
                return;
            }

            // Get scrapers from old AppConfiguration
            var config = await _configService.GetConfigurationAsync();
            if (config.Scrapers == null || !config.Scrapers.Any())
            {
                _logger.LogInformation("No scrapers found in AppConfiguration, nothing to migrate");
                return;
            }

            _logger.LogInformation("Migrating {Count} scrapers from AppConfiguration to ScraperConfigs table", config.Scrapers.Count);

            // Migrate each scraper
            foreach (var scraper in config.Scrapers)
            {
                try
                {
                    await _scraperConfigService.SaveScraperConfigAsync(scraper);
                    _logger.LogInformation("Migrated scraper: {ScraperName}", scraper.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error migrating scraper {ScraperName}", scraper.Name);
                }
            }

            // Clear scrapers from AppConfiguration (optional - keep for backward compatibility)
            // config.Scrapers.Clear();
            // await _configService.SaveConfigurationAsync(config);

            _logger.LogInformation("Migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scraper migration");
            throw;
        }
    }
}
