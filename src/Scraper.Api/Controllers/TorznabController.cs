using System.Text;
using Microsoft.AspNetCore.Mvc;
using Scraper.Api.Models;
using Scraper.Api.Services;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Serilog;

namespace Scraper.Api.Controllers;

[ApiController]
[Route("api")]
public class TorznabController : ControllerBase
{
    private readonly TorznabService _torznabService;
    private readonly IConfigurationService _configService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TorznabController(
        TorznabService torznabService, 
        IConfigurationService configService,
        IHttpContextAccessor httpContextAccessor)
    {
        _torznabService = torznabService;
        _configService = configService;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? t, [FromQuery] string? q, 
        [FromQuery] string? imdbid, [FromQuery] string? season, [FromQuery] string? episode,
        [FromQuery] string? apikey, [FromQuery] string? cat, [FromQuery] string? extended,
        [FromQuery] string? offset, [FromQuery] string? limit, [FromQuery] string? skipDatabase)
    {
        var typeLower = (t ?? string.Empty).ToLowerInvariant();
        var qValue = q ?? string.Empty;
        var imdbId = imdbid ?? string.Empty;
        var seasonValue = season ?? string.Empty;
        var episodeValue = episode ?? string.Empty;
        var apiKey = apikey ?? string.Empty;
        var catValue = cat ?? string.Empty;
        var extendedValue = extended ?? string.Empty;
        var offsetValue = offset ?? string.Empty;
        var limitValue = limit ?? string.Empty;
        var skipDatabaseValue = skipDatabase ?? string.Empty;

        // Validate API key if configured
        var config = await _configService.GetConfigurationAsync();
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey != config.ApiKey)
            {
                Log.Warning("Invalid or missing API key");
                return Unauthorized();
            }
        }

        // Handle caps endpoint (capabilities)
        if (typeLower == "caps")
        {
            return Content(GetCapsXml(), "application/xml");
        }

        //// Handle validation/test requests without query
        //// Prowlarr/Radarr/Sonarr send requests without 'q' or 'imdbid' to validate the indexer
        //if (string.IsNullOrEmpty(qValue) && string.IsNullOrEmpty(imdbId))
        //{
        //    Log.Information("Validation request received (no query) - returning empty RSS feed");
        //    // Return empty but valid RSS feed for validation
        //    var emptyRss = new TorznabRss
        //    {
        //        Channel = new TorznabChannel
        //        {
        //            Title = "Media Scraper",
        //            Description = "Torznab-compatible media scraper",
        //            Link = string.Empty,
        //            Language = "en-us",
        //            Items = new List<TorznabItem>()
        //        }
        //    };
        //    var xml = _torznabService.SerializeToXml(emptyRss);
        //    return Content(xml, "application/xml");
        //}

        // Determine media type
        MediaType? mediaType = null;
        if (typeLower == "movie" || !string.IsNullOrEmpty(imdbId))
        {
            mediaType = MediaType.Movie;
        }
        else if (typeLower == "tvsearch")
        {
            mediaType = MediaType.TvShow;
        }

        // Parse categories if provided (filter results by category)
        var categories = new List<int>();
        if (!string.IsNullOrEmpty(catValue))
        {
            var catParts = catValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var catPart in catParts)
            {
                if (int.TryParse(catPart.Trim(), out var catId))
                {
                    categories.Add(catId);
                }
            }
        }

        try
        {
            // Get base URL for enclosure generation
            var baseUrl = config.BaseUrlOverride;
            if (string.IsNullOrEmpty(baseUrl))
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                if (request != null)
                {
                    baseUrl = $"{request.Scheme}://{request.Host}";
                }
            }

            // Perform search
            var rss = await _torznabService.SearchAsync(qValue,
                imdbId,
                mediaType,
                baseUrl,
                offsetValue,
                limitValue,
                skipDatabaseValue,
                config.ApiKey);

            // Filter by categories if specified
            if (categories.Any())
            {
                var originalCount = rss.Channel.Items.Count;
                rss.Channel.Items = rss.Channel.Items.Where(item =>
                {
                    // Category 2000 = Movies, 5000 = TV
                    // Categories is a List<string>, so we need to parse them
                    var itemCategories = item.Categories ?? new List<string>();

                    // If item has no categories, include it (shouldn't happen, but be safe)
                    if (!itemCategories.Any())
                    {
                        Log.Warning("Item {Title} has no categories", item.Title);
                        return true; // Include items without categories
                    }

                    // Check if any of the item's categories match the requested categories
                    foreach (var catStr in itemCategories)
                    {
                        if (int.TryParse(catStr, out var catId))
                        {
                            // Match exact category or parent category (e.g., 2040 matches 2000)
                            if (categories.Contains(catId) ||
                                (catId >= 2000 && catId < 3000 && categories.Contains(2000)) || // Movies subcategory
                                (catId >= 5000 && catId < 6000 && categories.Contains(5000)))  // TV subcategory
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }).ToList();

                Log.Information("Filtered {OriginalCount} items to {FilteredCount} items based on categories {Categories}",
                    originalCount, rss.Channel.Items.Count, string.Join(",", categories));
            }

            // Serialize to XML
            var xml = _torznabService.SerializeToXml(rss);

            return Content(xml, "application/xml");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing Torznab request");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string? apikey, [FromQuery] string? link, [FromQuery] string? file)
    {
        try
        {
            // Validate API key
            var config = await _configService.GetConfigurationAsync();
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                if (string.IsNullOrEmpty(apikey) || apikey != config.ApiKey)
                {
                    Log.Warning("Invalid or missing API key for download");
                    return Unauthorized();
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                return BadRequest("Link parameter is required");
            }

            // Decode base64 link
            string decodedLink;
            try
            {
                var bytes = Convert.FromBase64String(link);
                decodedLink = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // If not base64, use as-is (might be URL encoded)
                decodedLink = Uri.UnescapeDataString(link);
            }

            // Redirect to the actual torrent/magnet link
            if (decodedLink.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(decodedLink);
            }
            else if (decodedLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                     decodedLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(decodedLink);
            }
            else
            {
                return BadRequest("Invalid link format");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing download request");
            return StatusCode(500, "Internal server error");
        }
    }

    private static string GetCapsXml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<caps>
	<server title=""Media Scraper"" />
	<limits default=""100"" max=""100"" />
	<searching>
		<search available=""yes"" supportedParams=""q"" searchEngine=""raw"" />
		<tv-search available=""yes"" supportedParams=""q,season,ep"" searchEngine=""raw"" />
		<movie-search available=""yes"" supportedParams=""q,imdbid"" searchEngine=""raw"" />
	</searching>
	<categories>
		<category id=""2000"" name=""Movies"">
			<subcat id=""2010"" name=""Movies/Foreign"" />
			<subcat id=""2030"" name=""Movies/SD"" />
			<subcat id=""2040"" name=""Movies/HD"" />
			<subcat id=""2045"" name=""Movies/UHD"" />
			<subcat id=""2060"" name=""Movies/3D"" />
			<subcat id=""2070"" name=""Movies/DVD"" />
		</category>
		<category id=""5000"" name=""TV"">
			<subcat id=""5030"" name=""TV/SD"" />
			<subcat id=""5040"" name=""TV/HD"" />
			<subcat id=""5070"" name=""TV/Anime"" />
			<subcat id=""5080"" name=""TV/Documentary"" />
		</category>
	</categories>
	<tags />
</caps>";
    }
}

