using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Data;
using Scraper.Infrastructure.Data.Entities;

namespace Scraper.Infrastructure.Services;

public class ConfigurationService : IConfigurationService
{
    private const string ConfigKey = "AppConfiguration";
    private readonly ScraperDbContext _context;
    private readonly ILogger<ConfigurationService> _logger;
    private AppConfiguration? _cachedConfig;
    private readonly object _lockObject = new();

    public ConfigurationService(ScraperDbContext context, ILogger<ConfigurationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AppConfiguration> GetConfigurationAsync()
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        lock (_lockObject)
        {
            if (_cachedConfig != null)
                return _cachedConfig;
        }

        try
        {
            var configEntity = await _context.Configurations
                .FirstOrDefaultAsync(c => c.Key == ConfigKey);

            if (configEntity != null)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                _cachedConfig = JsonSerializer.Deserialize<AppConfiguration>(configEntity.Value, options);
                
                if (_cachedConfig == null)
                {
                    _logger.LogWarning("Failed to deserialize configuration from database, using defaults");
                    _cachedConfig = new AppConfiguration();
                }
            }
            else
            {
                _cachedConfig = new AppConfiguration();
                _logger.LogInformation("Configuration not found in database, using defaults");
            }

            // Generate API key if not present
            if (string.IsNullOrWhiteSpace(_cachedConfig.ApiKey))
            {
                _cachedConfig.ApiKey = GenerateApiKey();
                _logger.LogInformation("Generated new API key");
                // Save immediately with new API key
                await SaveConfigurationAsync(_cachedConfig);
            }

            lock (_lockObject)
            {
                if (_cachedConfig != null)
                    return _cachedConfig;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from database, using defaults");
            _cachedConfig = new AppConfiguration();
            _cachedConfig.ApiKey = GenerateApiKey();
        }

        // Garantir que sempre retornamos uma configuração válida
        if (_cachedConfig == null)
        {
            _cachedConfig = new AppConfiguration();
            _cachedConfig.ApiKey = GenerateApiKey();
        }

        return _cachedConfig;
    }

    public async Task SaveConfigurationAsync(AppConfiguration config)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, options);

            var configEntity = await _context.Configurations
                .FirstOrDefaultAsync(c => c.Key == ConfigKey);

            if (configEntity != null)
            {
                configEntity.Value = json;
                configEntity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                configEntity = new ConfigurationEntity
                {
                    Key = ConfigKey,
                    Value = json,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Configurations.Add(configEntity);
            }

            await _context.SaveChangesAsync();

            lock (_lockObject)
            {
                _cachedConfig = config;
            }

            _logger.LogInformation("Configuration saved to database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to database");
            throw;
        }
    }

    public async Task<ScraperConfig?> GetScraperConfigAsync(string scraperName)
    {
        var config = await GetConfigurationAsync();
        return config.Scrapers.FirstOrDefault(s => s.Name.Equals(scraperName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpdateScraperConfigAsync(ScraperConfig scraperConfig)
    {
        var config = await GetConfigurationAsync();
        var existing = config.Scrapers.FirstOrDefault(s => s.Name.Equals(scraperConfig.Name, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.IsEnabled = scraperConfig.IsEnabled;
            existing.Settings = scraperConfig.Settings;
        }
        else
        {
            config.Scrapers.Add(scraperConfig);
        }

        await SaveConfigurationAsync(config);
    }

    private static string GenerateApiKey()
    {
        // Generate a 32-character API key similar to Jackett
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 32)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

