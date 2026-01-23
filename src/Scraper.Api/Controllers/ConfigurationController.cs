using Microsoft.AspNetCore.Mvc;
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
}

