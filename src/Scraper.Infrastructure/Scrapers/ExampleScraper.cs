using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Http;

namespace Scraper.Infrastructure.Scrapers;

/// <summary>
/// Example scraper implementation with mock HTML parsing.
/// This serves as a template for implementing real scrapers.
/// </summary>
public class ExampleScraper : BaseScraper
{
    private const string BaseUrl = "https://example-tracker.com";

    public ExampleScraper(
        ITitleNormalizer titleNormalizer,
        ILogger<ExampleScraper> logger)
        : base(
            HttpClientFactory.CreateClient(BaseUrl),
            titleNormalizer,
            logger)
    {
    }

    public override string Name => "ExampleTracker";
    public override bool IsEnabled => false; // Set to false to disable

    public override async Task<IEnumerable<MediaItem>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Searching {Query} on {ScraperName}", request.Query, Name);

        // Example: Build search URL
        var searchUrl = $"{BaseUrl}/search?q={Uri.EscapeDataString(request.Query)}";

        try
        {
            // In a real implementation, you would fetch the actual HTML
            // For this example, we'll use mock HTML
            var html = await GetMockHtmlAsync(request.Query);
            var doc = ParseHtml(html);

            var results = new List<MediaItem>();

            // Example: Parse search results
            // Adjust selectors based on actual website structure
            var resultNodes = doc.DocumentNode.SelectNodes("//div[@class='torrent-item']") 
                ?? doc.DocumentNode.SelectNodes("//tr[@class='torrent-row']")
                ?? Enumerable.Empty<HtmlNode>();

            foreach (var node in resultNodes)
            {
                try
                {
                    var item = ParseTorrentItem(node, request.Type ?? MediaType.Unknown);
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error parsing torrent item");
                }
            }

            Logger.LogInformation("Found {Count} results from {ScraperName}", results.Count, Name);
            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }
    }

    private MediaItem? ParseTorrentItem(HtmlNode node, MediaType defaultType)
    {
        try
        {
            // Example selectors - adjust based on actual HTML structure
            var titleNode = node.SelectSingleNode(".//a[@class='torrent-title']") 
                ?? node.SelectSingleNode(".//td[@class='title']//a");
            
            var linkNode = node.SelectSingleNode(".//a[contains(@href, 'magnet')]") 
                ?? node.SelectSingleNode(".//a[contains(@href, '.torrent')]")
                ?? titleNode;

            var sizeNode = node.SelectSingleNode(".//span[@class='size']") 
                ?? node.SelectSingleNode(".//td[@class='size']");

            var dateNode = node.SelectSingleNode(".//span[@class='date']") 
                ?? node.SelectSingleNode(".//td[@class='date']");

            if (titleNode == null || linkNode == null)
                return null;

            var title = titleNode.InnerText.Trim();
            var link = linkNode.GetAttributeValue("href", "");

            // Handle relative URLs
            if (!string.IsNullOrEmpty(link) && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                link = new Uri(new Uri(BaseUrl), link).ToString();
            }

            var sizeText = sizeNode?.InnerText.Trim();
            var dateText = dateNode?.InnerText.Trim();

            var item = CreateMediaItem(
                title,
                link,
                ParseFileSize(sizeText),
                ParseDate(dateText),
                defaultType
            );

            return item;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error parsing torrent item node");
            return null;
        }
    }

    /// <summary>
    /// Mock HTML for demonstration purposes.
    /// Replace this with actual HTTP requests in production.
    /// </summary>
    private Task<string> GetMockHtmlAsync(string query)
    {
        // This is mock HTML - in production, use FetchHtmlAsync with actual URL
        var mockHtml = $@"
<html>
<head><title>Search Results for {query}</title></head>
<body>
    <div class='torrent-item'>
        <a class='torrent-title' href='/torrent/123'>The Matrix (1999) [1080p] [PT-BR] [BluRay]</a>
        <span class='size'>8.5 GB</span>
        <span class='date'>2024-01-15</span>
        <a href='magnet:?xt=urn:btih:EXAMPLE123456789'>Magnet</a>
    </div>
    <div class='torrent-item'>
        <a class='torrent-title' href='/torrent/124'>Breaking Bad S01E01 [720p] [DUAL] [WEB-DL]</a>
        <span class='size'>1.2 GB</span>
        <span class='date'>2024-01-14</span>
        <a href='magnet:?xt=urn:btih:EXAMPLE987654321'>Magnet</a>
    </div>
</body>
</html>";

        return Task.FromResult(mockHtml);
    }
}

