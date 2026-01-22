using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFilePath;
    private readonly ILogger<ConfigurationService> _logger;
    private AppConfiguration? _cachedConfig;
    private readonly object _lockObject = new();

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        var configDir = Path.Combine(Directory.GetCurrentDirectory(), "config");
        Directory.CreateDirectory(configDir);
        _configFilePath = Path.Combine(configDir, "appsettings.json");
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

            if (File.Exists(_configFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_configFilePath);
                    _cachedConfig = JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration();
                    
                    // Generate API key if not present
                    if (string.IsNullOrWhiteSpace(_cachedConfig.ApiKey))
                    {
                        _cachedConfig.ApiKey = GenerateApiKey();
                        _logger.LogInformation("Generated new API key");
                        // Save immediately with new API key
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        var updatedJson = JsonSerializer.Serialize(_cachedConfig, options);
                        File.WriteAllText(_configFilePath, updatedJson);
                    }
                    
                    _logger.LogInformation("Loaded configuration from {Path}", _configFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading configuration, using defaults");
                    _cachedConfig = new AppConfiguration();
                    _cachedConfig.ApiKey = GenerateApiKey();
                }
            }
            else
            {
                _cachedConfig = new AppConfiguration();
                _cachedConfig.ApiKey = GenerateApiKey();
                _logger.LogInformation("Configuration file not found, using defaults with generated API key");
            }
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
            await File.WriteAllTextAsync(_configFilePath, json);

            lock (_lockObject)
            {
                _cachedConfig = config;
            }

            _logger.LogInformation("Configuration saved to {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
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

