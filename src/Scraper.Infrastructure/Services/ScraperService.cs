using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Interfaces;

namespace Scraper.Infrastructure.Services;

public class ScraperService : IScraperService
{
    private readonly DynamicScraperService _dynamicScraperService;
    private readonly ScraperConfigService _scraperConfigService;
    private readonly ILogger<ScraperService> _logger;
    private IEnumerable<IScraper>? _allScrapers;

    public ScraperService(
        DynamicScraperService dynamicScraperService,
        ScraperConfigService scraperConfigService,
        ILogger<ScraperService> logger)
    {
        _dynamicScraperService = dynamicScraperService;
        _scraperConfigService = scraperConfigService;
        _logger = logger;
    }

    private async Task<IEnumerable<IScraper>> GetAllScrapersAsync()
    {
        if (_allScrapers == null)
        {
            _allScrapers = await _dynamicScraperService.GetAllScrapersAsync();
        }
        return _allScrapers;
    }

    /// <summary>
    /// Clears the scraper cache (useful after configuration changes)
    /// </summary>
    public void ClearCache()
    {
        _allScrapers = null;
        _dynamicScraperService.ClearCache();
    }

    public async Task<IEnumerable<MediaItem>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var results = new List<MediaItem>();
        
        // Get all scrapers (static + dynamic)
        var allScrapers = await GetAllScrapersAsync();
        
        // Filter scrapers based on configuration if available
        var scrapersToUse = new List<IScraper>();
        foreach (var scraper in allScrapers)
        {
            if (!scraper.IsEnabled)
                continue;            

            // Check enabled status from database
            var dbConfig = await _scraperConfigService.GetScraperConfigAsync(scraper.Name);
            if (dbConfig != null && !dbConfig.IsEnabled)
                continue;

            scrapersToUse.Add(scraper);
        }
        
        _logger.LogInformation("Searching '{Query}' across '{Count}' scrapers", request.Query, scrapersToUse.Count);

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

