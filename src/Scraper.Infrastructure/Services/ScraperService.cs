using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Services;

public class ScraperService : IScraperService
{
    private readonly IEnumerable<IScraper> _scrapers;
    private readonly ILogger<ScraperService> _logger;
    private readonly IConfigurationService? _configService;

    public ScraperService(
        IEnumerable<IScraper> scrapers, 
        ILogger<ScraperService> logger,
        IConfigurationService? configService = null)
    {
        _scrapers = scrapers;
        _logger = logger;
        _configService = configService;
    }

    public async Task<IEnumerable<MediaItem>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<MediaItem>();
        
        // Filter scrapers based on configuration if available
        var scrapersToUse = new List<IScraper>();
        foreach (var scraper in _scrapers)
        {
            if (!scraper.IsEnabled)
                continue;
            
            // Check configuration if available
            if (_configService != null)
            {
                var config = await _configService.GetScraperConfigAsync(scraper.Name);
                if (config != null && !config.IsEnabled)
                    continue;
            }
            
            scrapersToUse.Add(scraper);
        }
        
        _logger.LogInformation("Searching {Query} across {Count} scrapers", request.Query, scrapersToUse.Count);

        var tasks = scrapersToUse.Select(async scraper =>
        {
            try
            {
                var items = await scraper.SearchAsync(request, cancellationToken);
                return items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching with scraper {ScraperName}", scraper.Name);
                return new List<MediaItem>();
            }
        });

        var scraperResults = await Task.WhenAll(tasks);
        results.AddRange(scraperResults.SelectMany(r => r));

        _logger.LogInformation("Found {Count} results for query {Query}", results.Count, request.Query);

        return results;
    }
}

