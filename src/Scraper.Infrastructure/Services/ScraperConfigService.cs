using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Data;
using Scraper.Infrastructure.Data.Entities;

namespace Scraper.Infrastructure.Services;

/// <summary>
/// Service for managing scraper configurations in a separate database table
/// </summary>
public class ScraperConfigService
{
    private readonly ScraperDbContext _context;
    private readonly ILogger<ScraperConfigService> _logger;

    public ScraperConfigService(
        ScraperDbContext context,
        ILogger<ScraperConfigService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all scraper configurations from database
    /// </summary>
    public async Task<List<ScraperConfig>> GetAllScraperConfigsAsync()
    {
        try
        {
            var entities = await _context.ScraperConfigs.ToListAsync();
            return entities.Select(e => e.ToScraperConfig()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all scraper configs from database");
            return new List<ScraperConfig>();
        }
    }

    /// <summary>
    /// Gets a specific scraper configuration by name
    /// </summary>
    public async Task<ScraperConfig?> GetScraperConfigAsync(string scraperName)
    {
        try
        {
            var scraperNameLower = scraperName.ToLowerInvariant();
            var entity = await _context.ScraperConfigs
                .FirstOrDefaultAsync(s => s.Name.ToLower() == scraperNameLower);

            return entity?.ToScraperConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scraper config {ScraperName} from database", scraperName);
            return null;
        }
    }

    /// <summary>
    /// Creates or updates a scraper configuration
    /// </summary>
    public async Task SaveScraperConfigAsync(ScraperConfig config)
    {
        try
        {
            var configNameLower = config.Name.ToLowerInvariant();
            var entity = await _context.ScraperConfigs
                .FirstOrDefaultAsync(s => s.Name.ToLower() == configNameLower);

            if (entity != null)
            {
                _logger.LogInformation("Updating scraper config: {ScraperName}", config.Name);
                entity.IsEnabled = config.IsEnabled;
                entity.SettingsJson = ScraperConfigEntity.FromScraperConfig(config).SettingsJson;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _logger.LogInformation("Creating new scraper config: {ScraperName}", config.Name);
                entity = ScraperConfigEntity.FromScraperConfig(config);
                _context.ScraperConfigs.Add(entity);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully saved scraper config: {ScraperName}", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving scraper config {ScraperName}", config.Name);
            throw;
        }
    }

    /// <summary>
    /// Deletes a scraper configuration
    /// </summary>
    public async Task<bool> DeleteScraperConfigAsync(string scraperName)
    {
        try
        {
            var scraperNameLower = scraperName.ToLowerInvariant();
            var entity = await _context.ScraperConfigs
                .FirstOrDefaultAsync(s => s.Name.ToLower() == scraperNameLower);

            if (entity != null)
            {
                _context.ScraperConfigs.Remove(entity);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted scraper config: {ScraperName}", scraperName);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scraper config {ScraperName}", scraperName);
            throw;
        }
    }

    /// <summary>
    /// Checks if a scraper configuration exists
    /// </summary>
    public async Task<bool> ScraperConfigExistsAsync(string scraperName)
    {
        try
        {
            var scraperNameLower = scraperName.ToLowerInvariant();
            return await _context.ScraperConfigs
                .AnyAsync(s => s.Name.ToLower() == scraperNameLower);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if scraper config exists: {ScraperName}", scraperName);
            return false;
        }
    }
}
