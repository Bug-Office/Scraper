using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IConfigurationService
{
    Task<AppConfiguration> GetConfigurationAsync();
    Task SaveConfigurationAsync(AppConfiguration config);
    Task<ScraperConfig?> GetScraperConfigAsync(string scraperName);
    Task UpdateScraperConfigAsync(ScraperConfig config);
}

