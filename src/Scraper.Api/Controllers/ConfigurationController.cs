using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Serilog;
using System.Text.Json;

namespace Scraper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _configService;

    public ConfigurationController(IConfigurationService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var config = await _configService.GetConfigurationAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading configuration");
            return StatusCode(500, "Failed to load configuration");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] AppConfiguration? config)
    {
        try
        {
            if (config == null)
            {
                // Try to read from request body if not bound
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();

                Log.Debug("Received configuration JSON: {Length} characters", json.Length);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                config = JsonSerializer.Deserialize<AppConfiguration>(json, options);

                if (config == null)
                {
                    Log.Warning("Failed to deserialize configuration - result is null");
                    return BadRequest("Invalid configuration data: deserialization returned null");
                }
            }

            // Ensure API key is preserved - always use existing key if new one is empty
            var currentConfig = await _configService.GetConfigurationAsync();
            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                // If no API key provided, use existing one or generate new
                if (!string.IsNullOrWhiteSpace(currentConfig.ApiKey))
                {
                    config.ApiKey = currentConfig.ApiKey;
                    Log.Information("Preserved existing API key");
                }
                else
                {
                    // Generate new API key if none exists
                    const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                    var random = new Random();
                    config.ApiKey = new string(Enumerable.Repeat(chars, 32)
                        .Select(s => s[random.Next(s.Length)]).ToArray());
                    Log.Information("Generated new API key");
                }
            }

            Log.Information("Saving configuration with {ScraperCount} scrapers", config.Scrapers?.Count ?? 0);
            await _configService.SaveConfigurationAsync(config);

            return Ok(new { message = "Configuration saved successfully" });
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "JSON deserialization error: {Message}", ex.Message);
            return BadRequest($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving configuration: {Message}", ex.Message);
            return StatusCode(500, $"Failed to save configuration: {ex.Message}");
        }
    }

    [HttpGet("generate-apikey")]
    public IActionResult GenerateApiKey()
    {
        // Generate a new API key
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var apiKey = new string(Enumerable.Repeat(chars, 32)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        return Ok(new { apiKey });
    }

    [HttpGet("scraper/{scraperName}")]
    public async Task<IActionResult> GetScraperConfiguration(string scraperName)
    {
        try
        {
            var configService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.ScraperConfigurationService>();
            var config = await configService.GetScraperConfigurationAsync(scraperName);
            return Ok(config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading scraper configuration for {ScraperName}", scraperName);
            return StatusCode(500, $"Failed to load configuration: {ex.Message}");
        }
    }

    [HttpPost("scraper/{scraperName}")]
    public async Task<IActionResult> SaveScraperConfiguration(string scraperName, [FromBody] Infrastructure.Configurations.ScraperConfiguration? configuration)
    {
        try
        {
            if (configuration == null)
            {
                return BadRequest("Configuration is required");
            }

            var configService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.ScraperConfigurationService>();
            await configService.SaveScraperConfigurationAsync(scraperName, configuration);
            
            // Clear cache in DynamicScraperService and ScraperService
            var dynamicScraperService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.DynamicScraperService>();
            dynamicScraperService.ClearCache();
            var scraperService = HttpContext.RequestServices.GetRequiredService<IScraperService>() as Scraper.Infrastructure.Services.ScraperService;
            scraperService?.ClearCache();
            
            return Ok(new { message = "Scraper configuration saved successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving scraper configuration for {ScraperName}", scraperName);
            return StatusCode(500, $"Failed to save configuration: {ex.Message}");
        }
    }

    [HttpPost("scraper/{scraperName}/toggle")]
    public async Task<IActionResult> ToggleScraper(string scraperName, [FromBody] ToggleScraperRequest? request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Request body is required");
            }

            var scraperConfigService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.ScraperConfigService>();
            var scraperConfig = await scraperConfigService.GetScraperConfigAsync(scraperName);

            if (scraperConfig == null)
            {
                return NotFound($"Scraper {scraperName} not found");
            }

            scraperConfig.IsEnabled = request.IsEnabled;
            await scraperConfigService.SaveScraperConfigAsync(scraperConfig);

            // Clear cache
            var scraperService = HttpContext.RequestServices.GetRequiredService<IScraperService>() as Scraper.Infrastructure.Services.ScraperService;
            scraperService?.ClearCache();

            return Ok(new { message = $"Scraper {scraperName} {(request.IsEnabled ? "enabled" : "disabled")} successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling scraper {ScraperName}", scraperName);
            return StatusCode(500, $"Failed to toggle scraper: {ex.Message}");
        }
    }

    [HttpPost("scraper")]
    public async Task<IActionResult> CreateScraper([FromBody] CreateScraperRequest? request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Scraper name is required");
            }

            if (request.Configuration == null)
            {
                return BadRequest("Configuration is required");
            }

            var dynamicScraperService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.DynamicScraperService>();
            var success = await dynamicScraperService.CreateScraperAsync(request.Name, request.Configuration, request.IsEnabled ?? true);

            if (success)
            {
                // Clear cache
                var scraperService = HttpContext.RequestServices.GetRequiredService<IScraperService>() as Scraper.Infrastructure.Services.ScraperService;
                scraperService?.ClearCache();
                
                return Ok(new { message = $"Scraper {request.Name} created successfully" });
            }
            else
            {
                return BadRequest($"Failed to create scraper {request.Name}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating scraper");
            return StatusCode(500, $"Failed to create scraper: {ex.Message}");
        }
    }

    [HttpDelete("scraper/{scraperName}")]
    public async Task<IActionResult> DeleteScraper(string scraperName)
    {
        try
        {
            var dynamicScraperService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.DynamicScraperService>();
            var success = await dynamicScraperService.DeleteScraperAsync(scraperName);

            if (success)
            {
                // Clear cache
                var scraperService = HttpContext.RequestServices.GetRequiredService<IScraperService>() as Scraper.Infrastructure.Services.ScraperService;
                scraperService?.ClearCache();
                
                return Ok(new { message = $"Scraper {scraperName} deleted successfully" });
            }
            else
            {
                return BadRequest($"Failed to delete scraper {scraperName} or scraper not found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting scraper {ScraperName}", scraperName);
            return StatusCode(500, $"Failed to delete scraper: {ex.Message}");
        }
    }

    [HttpGet("scraper-template")]
    public async Task<IActionResult> GetDefaultTemplate()
    {
        try
        {
            var initializationService = HttpContext.RequestServices.GetRequiredService<Scraper.Infrastructure.Services.ScraperInitializationService>();
            var defaultScrapersPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "default-scrapers.json");
            var template = await initializationService.GetDefaultTemplateAsync(defaultScrapersPath);

            if (template == null)
            {
                return NotFound("Default template not found");
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading default template");
            return StatusCode(500, $"Failed to load template: {ex.Message}");
        }
    }

    public class CreateScraperRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool? IsEnabled { get; set; }
        public Scraper.Infrastructure.Configurations.ScraperConfiguration? Configuration { get; set; }
    }

    public class ToggleScraperRequest
    {
        public bool IsEnabled { get; set; }
    }
}

