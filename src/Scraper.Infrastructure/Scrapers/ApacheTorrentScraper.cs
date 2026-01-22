using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Http;
using System.Text.RegularExpressions;

namespace Scraper.Infrastructure.Scrapers;

/// <summary>
/// Scraper for Apache Torrent (https://apachetorrent.com)
/// Supports movies and series with PT-BR, DUAL, and LEGENDADO releases
/// </summary>
public class ApacheTorrentScraper : BaseScraper
{
    private const string BaseUrl = "https://apachetorrent.com";
    private static readonly Regex YearRegex = new(@"\(Filme de (\d{4})\)|\(Série de (\d{4})\)", RegexOptions.Compiled);
    private static readonly Regex SeriesRegex = new(@"(?i)(s\d{1,2}e\d{1,2}|season\s*\d+|temporada\s*\d+)", RegexOptions.Compiled);

    public ApacheTorrentScraper(
        ITitleNormalizer titleNormalizer,
        ILogger<ApacheTorrentScraper> logger,
        IFlareSolverrService? flareSolverrService = null)
        : base(
            HttpClientFactory.CreateClient(BaseUrl),
            titleNormalizer,
            logger,
            flareSolverrService)
    {
    }

    public override string Name => "ApacheTorrent";
    public override bool IsEnabled => true;

    public override async Task<IEnumerable<MediaItem>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Searching {Query} on {ScraperName}", request.Query, Name);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Enumerable.Empty<MediaItem>();
        }

        try
        {
            // Build search URL: https://apachetorrent.com/index.php?s={query}
            var searchUrl = $"{BaseUrl}/index.php?s={Uri.EscapeDataString(request.Query)}";
            
            Logger.LogDebug("Fetching search results from {Url}", searchUrl);
            string html;
            try
            {
                html = await FetchHtmlAsync(searchUrl, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                Logger.LogError(ex, "Timeout while fetching search results from {Url}. The website may be slow or unresponsive.", searchUrl);
                return Enumerable.Empty<MediaItem>();
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError(ex, "Request canceled while fetching search results from {Url}", searchUrl);
                return Enumerable.Empty<MediaItem>();
            }
            
            var doc = ParseHtml(html);

            var results = new List<MediaItem>();

            // Apache Torrent uses div with class "capaname" for each result item
            var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'capaname')]")
                ?? doc.DocumentNode.SelectNodes("//article") 
                ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'post')]")
                ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'item')]")
                ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'torrent')]")
                ?? Enumerable.Empty<HtmlNode>();

            // Parse items in parallel for better performance
            var parseTasks = resultNodes.Select(async node =>
            {
                try
                {
                    var items = await ParseTorrentItemAsync(node, cancellationToken);
                    return items;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error parsing torrent item from Apache Torrent");
                    return Enumerable.Empty<MediaItem>();
                }
            });

            var parsedItems = await Task.WhenAll(parseTasks);
            var allItems = parsedItems.SelectMany(items => items);
            results.AddRange(allItems.Where(item => !request.Type.HasValue || item.Type == request.Type));

            Logger.LogInformation("Found {Count} results from {ScraperName}", results.Count, Name);
            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }
    }

    private async Task<IEnumerable<MediaItem>> ParseTorrentItemAsync(HtmlNode node, CancellationToken cancellationToken)
    {
        try
        {
            // Apache Torrent structure: div.capaname > a (image link) and h2 > a (title link)
            // Both links point to the same detail page
            var detailLinkNode = node.SelectSingleNode(".//h2//a")
                ?? node.SelectSingleNode(".//h2/a")
                ?? node.SelectSingleNode(".//a[contains(@href, 'apachetorrent.com')]")
                ?? node.SelectSingleNode(".//a[contains(@href, '/')]")
                ?? (node.Name == "a" ? node : null);

            if (detailLinkNode == null)
            {
                // Try to find any link in the node
                var anyLink = node.SelectSingleNode(".//a");
                if (anyLink == null)
                {
                    return Enumerable.Empty<MediaItem>();
                }
                detailLinkNode = anyLink;
            }

            // Get title text - usually in h2 > a
            var titleText = detailLinkNode.InnerText.Trim();
            
            // If title is empty, try getting from h2 directly
            if (string.IsNullOrWhiteSpace(titleText))
            {
                var h2Node = node.SelectSingleNode(".//h2");
                if (h2Node != null)
                {
                    titleText = h2Node.InnerText.Trim();
                }
            }

            // Clean title - remove HTML entities, <br> tags, and extra whitespace
            titleText = System.Net.WebUtility.HtmlDecode(titleText);
            titleText = Regex.Replace(titleText, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
            titleText = Regex.Replace(titleText, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(titleText))
            {
                return Enumerable.Empty<MediaItem>();
            }

            // Get the detail page link
            var detailLink = detailLinkNode.GetAttributeValue("href", "");
            
            // If we have a relative link, make it absolute
            if (!string.IsNullOrEmpty(detailLink) && !detailLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                detailLink = new Uri(new Uri(BaseUrl), detailLink).ToString();
            }

            // If still no link, try the first anchor in the node
            if (string.IsNullOrEmpty(detailLink))
            {
                var firstLink = node.SelectSingleNode(".//a");
                if (firstLink != null)
                {
                    detailLink = firstLink.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(detailLink) && !detailLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        detailLink = new Uri(new Uri(BaseUrl), detailLink).ToString();
                    }
                }
            }

            // Try to find magnet or download link directly on search results page
            var magnetLink = node.SelectSingleNode(".//a[contains(@href, 'magnet:')]")
                ?? node.SelectSingleNode("//a[contains(@href, 'magnet:')]");
            
            string? link = null;
            if (magnetLink != null)
            {
                link = magnetLink.GetAttributeValue("href", "");
            }

            // Try to find torrent download link on search page
            if (string.IsNullOrEmpty(link))
            {
                var torrentLink = node.SelectSingleNode(".//a[contains(@href, '.torrent')]")
                    ?? node.SelectSingleNode(".//a[contains(text(), 'Download')]")
                    ?? node.SelectSingleNode(".//a[contains(text(), 'Baixar')]");

                if (torrentLink != null)
                {
                    link = torrentLink.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(link) && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        link = new Uri(new Uri(BaseUrl), link).ToString();
                    }
                }
            }

            // If no direct link found, try to fetch the detail page to get the actual torrent/magnet link
            if (string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(detailLink))
            {
                try
                {
                    link = await FetchTorrentLinkFromDetailPageAsync(detailLink, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to fetch torrent link from detail page {Url}", detailLink);
                    // Fallback to detail page link
                    link = detailLink;
                }
            }

            // Final fallback
            if (string.IsNullOrEmpty(link))
            {
                link = detailLink;
            }

            // Extract size from text (look for GB, MB patterns)
            var sizeText = ExtractSizeFromText(node.InnerText);
            
            // Extract date - look for date patterns in the text
            var dateText = ExtractDateFromText(node.InnerText);

            // Determine media type
            var mediaType = DetermineMediaType(titleText, node.InnerText);

            // Clean title - remove "Torrent Download" and similar text
            titleText = CleanTitle(titleText);

            // For TV series, check if detail page has multiple episodes
            if (mediaType == MediaType.TvShow && !string.IsNullOrEmpty(detailLink))
            {
                try
                {
                    var episodes = await ExtractEpisodesFromDetailPageAsync(detailLink, titleText, cancellationToken);
                    if (episodes.Any())
                    {
                        Logger.LogDebug("Found {Count} episodes for series {Title}", episodes.Count(), titleText);
                        return episodes;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error extracting episodes from detail page, falling back to single item");
                }
            }

            // Single item (movie or single episode)
            var item = CreateMediaItem(
                titleText,
                link,
                ParseFileSize(sizeText),
                ParseDate(dateText),
                mediaType
            );

            return new[] { item };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error parsing torrent item node from Apache Torrent");
            return Enumerable.Empty<MediaItem>();
        }
    }

    private string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        // Remove common suffixes
        title = Regex.Replace(title, @"(?i)\s*Torrent\s*Download\s*$", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"(?i)\s*Download\s*$", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"(?i)\s*Baixar\s*$", "", RegexOptions.IgnoreCase);
        
        // Remove year pattern if it's at the end: (Filme de 2025)
        title = Regex.Replace(title, @"\s*\(Filme de \d{4}\)\s*$", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\s*\(Série de \d{4}\)\s*$", "", RegexOptions.IgnoreCase);

        return title.Trim();
    }

    private string? ExtractSizeFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Look for size patterns: "8.5 GB", "1.2 GB", "500 MB"
        var sizeMatch = Regex.Match(text, @"(\d+[\.,]?\d*)\s*(GB|MB|KB)", RegexOptions.IgnoreCase);
        if (sizeMatch.Success)
        {
            return $"{sizeMatch.Groups[1].Value.Replace(',', '.')} {sizeMatch.Groups[2].Value.ToUpper()}";
        }

        return null;
    }

    private string? ExtractDateFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Look for year in "Filme de YYYY" or "Série de YYYY"
        var yearMatch = YearRegex.Match(text);
        if (yearMatch.Success)
        {
            var year = yearMatch.Groups[1].Success ? yearMatch.Groups[1].Value : yearMatch.Groups[2].Value;
            // Return as date (use January 1st of that year)
            return $"{year}-01-01";
        }

        return null;
    }

    private MediaType DetermineMediaType(string title, string fullText)
    {

        var lowerTitle = title.ToLowerInvariant();
        var lowerText = fullText.ToLowerInvariant();

        // Check for series indicators
        if (SeriesRegex.IsMatch(lowerTitle) || SeriesRegex.IsMatch(lowerText) ||
            lowerTitle.Contains("série") || lowerText.Contains("série") ||
            lowerTitle.Contains("temporada") || lowerText.Contains("temporada"))
        {
            return MediaType.TvShow;
        }

        // Check for movie indicators
        if (lowerTitle.Contains("filme") || lowerText.Contains("filme"))
        {
            return MediaType.Movie;
        }

        // Default to movie if uncertain
        return MediaType.Movie;
    }

    /// <summary>
    /// Extracts all episodes from a TV series detail page
    /// </summary>
    private async Task<IEnumerable<MediaItem>> ExtractEpisodesFromDetailPageAsync(string detailUrl, string seriesTitle, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogDebug("Extracting episodes from detail page: {Url}", detailUrl);
            string html;
            try
            {
                html = await FetchHtmlAsync(detailUrl, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                Logger.LogWarning(ex, "Timeout while fetching episodes from detail page {Url}. Returning empty list.", detailUrl);
                return Enumerable.Empty<MediaItem>();
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogWarning(ex, "Request canceled while fetching episodes from detail page {Url}. Returning empty list.", detailUrl);
                return Enumerable.Empty<MediaItem>();
            }
            
            var doc = ParseHtml(html);

            var episodes = new List<MediaItem>();

            // Find all magnet links in the download section
            // Structure: <p class="text-center">EPISÓDIO...<a href="magnet:...">DOWNLOAD TORRENT</a></p>
            var downloadSection = doc.DocumentNode.SelectSingleNode("//div[@id='download']")
                ?? doc.DocumentNode.SelectSingleNode("//div[@id='lista_links']")
                ?? doc.DocumentNode;

            // Find all paragraphs containing episode information
            var episodeParagraphs = downloadSection.SelectNodes(".//p[@class='text-center']")
                ?? downloadSection.SelectNodes(".//p[contains(., 'EPISÓDIO')]")
                ?? Enumerable.Empty<HtmlNode>();

            foreach (var paragraph in episodeParagraphs)
            {
                try
                {
                    // Find magnet link in this paragraph
                    var episodeLink = paragraph.SelectSingleNode(".//a[contains(@href, 'magnet:')]");
                    if (episodeLink == null)
                        continue;

                    var magnet = episodeLink.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(magnet))
                        continue;

                    var episodeText = paragraph.InnerText;
                    
                    // Extract episode number from text (e.g., "01º EPISÓDIO", "02º EPISÓDIO")
                    var episodeMatch = Regex.Match(episodeText, @"(\d{1,2})[º°]\s*EPIS[ÓO]DIO", RegexOptions.IgnoreCase);
                    var episodeNumber = episodeMatch.Success ? episodeMatch.Groups[1].Value : null;

                    // Try to extract from magnet link dn parameter (e.g., "S02E01")
                    string? seasonNumber = null;
                    if (string.IsNullOrEmpty(episodeNumber))
                    {
                        var dnMatch = Regex.Match(magnet, @"dn=.*?S(\d{1,2})E(\d{1,2})", RegexOptions.IgnoreCase);
                        if (dnMatch.Success)
                        {
                            seasonNumber = dnMatch.Groups[1].Value;
                            episodeNumber = dnMatch.Groups[2].Value;
                        }
                    }

                    // If still no episode number, try to extract from series title or default
                    if (string.IsNullOrEmpty(episodeNumber))
                    {
                        // Try to find episode number from paragraph text in different formats
                        var altMatch = Regex.Match(episodeText, @"(\d{1,2})[º°]?\s*(?:EPIS[ÓO]DIO|EP)", RegexOptions.IgnoreCase);
                        if (altMatch.Success)
                        {
                            episodeNumber = altMatch.Groups[1].Value;
                        }
                    }

                    // Extract season number from series title if not found
                    if (string.IsNullOrEmpty(seasonNumber))
                    {
                        var seasonMatch = Regex.Match(seriesTitle, @"(\d{1,2})[ªa]\s*Temporada", RegexOptions.IgnoreCase);
                        seasonNumber = seasonMatch.Success ? seasonMatch.Groups[1].Value : "1"; // Default to 1 if not found
                    }

                    // Build episode title
                    // Remove season info from series title if present to avoid duplication
                    var cleanSeriesTitle = Regex.Replace(seriesTitle, @"\s*\d+[ªa]\s*Temporada\s*", " ", RegexOptions.IgnoreCase).Trim();
                    cleanSeriesTitle = Regex.Replace(cleanSeriesTitle, @"\s*Torrent\s*Download\s*$", "", RegexOptions.IgnoreCase).Trim();
                    
                    var episodeTitle = cleanSeriesTitle;
                    if (!string.IsNullOrEmpty(episodeNumber))
                    {
                        episodeTitle = $"{cleanSeriesTitle} S{seasonNumber.PadLeft(2, '0')}E{episodeNumber.PadLeft(2, '0')}";
                    }
                    else
                    {
                        // If we can't determine episode number, still create item but log warning
                        Logger.LogWarning("Could not determine episode number for magnet link in {Url}", detailUrl);
                        // Use a generic title
                        episodeTitle = $"{cleanSeriesTitle} Episode";
                    }

                    // Extract date from detail page
                    var dateText = ExtractDateFromText(doc.DocumentNode.InnerText);
                    
                    // Extract size if available
                    var sizeText = ExtractSizeFromText(episodeText);

                    var episode = CreateMediaItem(
                        episodeTitle,
                        magnet,
                        ParseFileSize(sizeText),
                        ParseDate(dateText),
                        MediaType.TvShow
                    );

                    episodes.Add(episode);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error parsing episode from detail page");
                }
            }

            return episodes;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting episodes from detail page {Url}", detailUrl);
            return Enumerable.Empty<MediaItem>();
        }
    }

    /// <summary>
    /// Fetches the detail page and extracts the magnet or torrent download link
    /// </summary>
    private async Task<string?> FetchTorrentLinkFromDetailPageAsync(string detailUrl, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogDebug("Fetching detail page for torrent link: {Url}", detailUrl);
            string html;
            try
            {
                html = await FetchHtmlAsync(detailUrl, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                Logger.LogWarning(ex, "Timeout while fetching detail page {Url}. Skipping this item.", detailUrl);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogWarning(ex, "Request canceled while fetching detail page {Url}. Skipping this item.", detailUrl);
                return null;
            }
            
            var doc = ParseHtml(html);

            // Try to find magnet link first
            var magnetLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'magnet:')]");
            if (magnetLink != null)
            {
                var magnet = magnetLink.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(magnet))
                {
                    Logger.LogDebug("Found magnet link on detail page");
                    return magnet;
                }
            }

            // Try to find torrent download link
            var torrentLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '.torrent')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'download')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'baixar')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Download')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Baixar')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Magnet')]");

            if (torrentLink != null)
            {
                var torrent = torrentLink.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(torrent))
                {
                    if (!torrent.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        torrent = new Uri(new Uri(BaseUrl), torrent).ToString();
                    }
                    Logger.LogDebug("Found torrent link on detail page");
                    return torrent;
                }
            }

            Logger.LogDebug("No torrent/magnet link found on detail page");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error fetching torrent link from detail page {Url}", detailUrl);
            return null;
        }
    }
}

