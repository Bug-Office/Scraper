using Microsoft.AspNetCore.Mvc;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Serilog;

namespace Scraper.Api.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public ApiController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpGet("scrapers")]
    public async Task<IActionResult> GetScrapers()
    {
        try
        {
            var dynamicScraperService = _serviceProvider.GetRequiredService<Scraper.Infrastructure.Services.DynamicScraperService>();
            var allScrapers = await dynamicScraperService.GetAllScrapersAsync();
            
            var scraperConfigService = _serviceProvider.GetRequiredService<Scraper.Infrastructure.Services.ScraperConfigService>();
            var allScraperConfigs = await scraperConfigService.GetAllScraperConfigsAsync();
            
            var scrapers = allScrapers.Select(s =>
            {
                var dbConfig = allScraperConfigs.FirstOrDefault(sc => sc.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
                return new
                {
                    name = s.Name,
                    isEnabled = dbConfig?.IsEnabled ?? s.IsEnabled
                };
            }).ToList();

            return Ok(scrapers);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error listing scrapers");
            return StatusCode(500, "Failed to list scrapers");
        }
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        try
        {
            var scraperService = _serviceProvider.GetRequiredService<IScraperService>();
            var testRequest = new SearchRequest { Query = "test" };
            var results = await scraperService.SearchAsync(testRequest);

            return Ok(new
            {
                success = true,
                message = $"Test successful. Found {results.Count()} results.",
                resultsCount = results.Count()
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Connection test failed");
            return Ok(new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}

