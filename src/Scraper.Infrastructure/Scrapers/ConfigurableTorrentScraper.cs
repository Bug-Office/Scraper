using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;
using Scraper.Infrastructure.Http;
using Scraper.Infrastructure.Interfaces;
using Scraper.Infrastructure.Parsers;
using System.Text.RegularExpressions;
using System.Threading;

namespace Scraper.Infrastructure.Scrapers;

/// <summary>
/// Generic configurable scraper for torrent sites
/// Can be configured for different sites by providing a ScraperConfiguration
/// </summary>
public class ConfigurableTorrentScraper : BaseScraper
{
    private static readonly Regex YearRegex = new(@"\(Filme de (\d{4})\)|\(Série de (\d{4})\)|\((\d{4})\)", RegexOptions.Compiled);
    private static readonly Regex SeriesRegex = new(@"(?i)(s\d{1,2}e\d{1,2}|season\s*\d+|temporada\s*\d+)", RegexOptions.Compiled);

    private readonly ScraperConfiguration _configuration;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILinkExtractor _linkExtractor;
    private readonly IEpisodeExtractor _episodeExtractor;
    private readonly IDetailPageParser _detailPageParser;
    private readonly string _scraperName;

    public ConfigurableTorrentScraper(
        string scraperName,
        ScraperConfiguration configuration,
        ITitleNormalizer titleNormalizer,
        ILogger logger,
        ITmdbService tmdbService,
        ILoggerFactory loggerFactory,
        IFlareSolverrService flareSolverrService,
        IMediaItemRepository mediaItemRepository)
        : base(
            HttpClientFactory.CreateClient(configuration.BaseUrl),
            titleNormalizer,
            logger,
            tmdbService,
            flareSolverrService,
            mediaItemRepository)
    {
        _scraperName = scraperName;
        _configuration = configuration;
        _metadataExtractor = new BaseMetadataExtractor();
        _linkExtractor = new BaseLinkExtractor();
        _episodeExtractor = new BaseEpisodeExtractor(
            loggerFactory.CreateLogger<BaseEpisodeExtractor>(),
            _configuration,
            _metadataExtractor,
            tmdbService,
            titleNormalizer);
        _detailPageParser = new BaseDetailPageParser(
            loggerFactory.CreateLogger<BaseDetailPageParser>(),
            _configuration,
            _metadataExtractor);
    }

    public override string Name => _scraperName;
    public override bool IsEnabled => true;

    public override async Task<IEnumerable<MediaItem>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Searching Query: '{Query}' | ImdbId: '{ImdbId}' on '{ScraperName}'", request.Query, request.ImdbId, Name);

        try
        {
            if (!string.IsNullOrEmpty(request.ImdbId))
            {
                var results = await SearchByImdbIdAsync(request, cancellationToken = default);
                return results;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }

        try
        {
            var tmdbmovieDetails = TmdbService.GetTmdbDetailsByTitleAsync(request.Query, null, request.Type).GetAwaiter().GetResult();
            request.Query = tmdbmovieDetails?.Name ?? request.Query;

            var results = await SearchByQueryAsync(request, cancellationToken = default);
            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }
    }

    public async Task<IEnumerable<MediaItem>> SearchByImdbIdAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {

        try
        {
            if (!string.IsNullOrEmpty(request.ImdbId))
            {
                var results = await MediaItemRepository!.GetByImdbId(request.ImdbId);
                if (results.Any())
                {
                    return results;
                }

                var tmdbmovieDetails = TmdbService.GetTmdbMovieDetailsByExternalSourceAsync(request.ImdbId, "imdb_id").GetAwaiter().GetResult();
                request.Query = tmdbmovieDetails?.Title;

                results = await SearchByQueryAsync(request, cancellationToken);
                return results;
            }
            return Enumerable.Empty<MediaItem>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }

    }

    public async Task<IEnumerable<MediaItem>> SearchByQueryAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {

        try
        {
            var searchUrl = BuildSearchUrl(request.Query);

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
            var resultNodes = FindResultNodes(doc);

            // Parse items in parallel for better performance (but don't save yet)
            var parseTasks = resultNodes.Select(async node =>
            {
                try
                {
                    var items = await ParseTorrentItemAsync(node, cancellationToken);
                    return items;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error parsing torrent item from {ScraperName}", Name);
                    return Enumerable.Empty<MediaItem>();
                }
            });

            var parsedItems = await Task.WhenAll(parseTasks);
            var allItems = parsedItems.SelectMany(items => items)
                                    .Where(item => !request.Type.HasValue || item.Type == request.Type)
                                    .ToList();

            // Filter out items that already exist in database and save new ones
            if (!request.SkipDatabase && allItems.Any())
            {
                var pageUrls = allItems.Select(i => i.PageUrl).Distinct().ToList();
                var existingItems = new List<MediaItem>();

                // Check existence sequentially to avoid DbContext concurrency issues
                foreach (var url in pageUrls)
                {
                    try
                    {
                        var existing = await MediaItemRepository!.GetByPageUrlAsync(url);
                        if (existing.Any())
                        {
                            existingItems.AddRange(existing);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Error checking existing items for URL: {Url}", url);
                    }
                }

                var existingPageUrls = existingItems.Select(i => i.PageUrl).ToHashSet();
                var newItems = allItems.Where(item => !existingPageUrls.Contains(item.PageUrl)).ToList();

                // Save only new items in batch to avoid DbContext concurrency issues
                if (newItems.Any())
                {
                    try
                    {
                        await MediaItemRepository!.SaveRangeAsync(newItems);
                        Logger.LogDebug("Saved {Count} new items to database in batch", newItems.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to save items to database in batch");
                    }
                }

                // Combine existing and new items
                allItems = existingItems.Concat(newItems).ToList();
            }

            var paginatedItems = allItems.Skip(request.Offset).Take(request.Limit);
            Logger.LogInformation("Found {Count} results from {ScraperName}", paginatedItems.Count(), Name);
            return paginatedItems;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }
    }


    private string BuildSearchUrl(string query)
    {
        return _configuration.SearchUrlTemplate
            .Replace("{BaseUrl}", _configuration.BaseUrl)
            .Replace("{Query}", Uri.EscapeDataString(query));
    }

    private IEnumerable<HtmlNode> FindResultNodes(HtmlDocument doc)
    {
        foreach (var selector in _configuration.ResultItemSelectors)
        {
            var nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes != null && nodes.Any())
                return nodes;
        }
        return Enumerable.Empty<HtmlNode>();
    }

    private async Task<IEnumerable<MediaItem>> ParseTorrentItemAsync(HtmlNode node, CancellationToken cancellationToken, bool skipDatabase = false)
    {
        try
        {
            var detailLinkNode = FindTitleLink(node);
            if (detailLinkNode == null)
            {
                return Enumerable.Empty<MediaItem>();
            }

            var titleText = ExtractTitle(node);
            if (string.IsNullOrWhiteSpace(titleText))
            {
                return Enumerable.Empty<MediaItem>();
            }

            var detailLink = ExtractDetailLink(detailLinkNode, node);
            var link = await ExtractDownloadLink(node, detailLink, cancellationToken);

            var sizeText = _metadataExtractor.ExtractSize(node.InnerText);
            var dateText = ExtractDateFromText(node.InnerText);
            var mediaType = DetermineMediaType(titleText, node.InnerText);
            titleText = CleanTitle(titleText);

            // Check if already exists in database (only if skipDatabase is false)
            if (!skipDatabase)
            {
                var existingItems = await MediaItemRepository.GetByPageUrlAsync(detailLink);
                if (existingItems.Any())
                {
                    Logger.LogDebug("Found existing items in database for URL: {Url}", detailLink);
                    return existingItems;
                }
            }

            // For TV series, check if detail page has multiple episodes
            if (mediaType == MediaType.TvShow)
                return await CreateEpisodeMediaItem(titleText, detailLink);

            if (mediaType == MediaType.Movie)
                return await CreateMovieMediaItem(
                    cancellationToken,
                    titleText,
                    detailLink,
                    link ?? detailLink,
                    size: _detailPageParser.ParseFileSize(sizeText),
                    ReleaseDate: _detailPageParser.ParseDate(dateText),
                    type: mediaType);

            return Enumerable.Empty<MediaItem>();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error parsing torrent item node from {ScraperName}", Name);
            return Enumerable.Empty<MediaItem>();
        }
    }

    private async Task<IEnumerable<MediaItem>> CreateMovieMediaItem(
        CancellationToken cancellationToken,
        string title,
        string pageUrl,
        string link,
        long? size = null,
        DateTime? ReleaseDate = null,
        MediaType type = MediaType.Unknown
    )
    {
        // Single item (movie or single episode)
        var item = CreateMediaItem(title, pageUrl, link, size, ReleaseDate, type);

        // Enrich with detail page information
        if (!string.IsNullOrEmpty(pageUrl))
        {
            try
            {
                var html = await FetchHtmlAsync(pageUrl, cancellationToken);
                _detailPageParser.EnrichMediaItem(item, pageUrl, html, cancellationToken);
                NormalizeMediaItem(item);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to enrich item from detail page {Url}", pageUrl);
            }
        }

        try
        {
            await MediaItemRepository.SaveAsync(item);
            Logger.LogDebug("Saved item to database: {Title}", item.Title);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save item to database: {Title}", item.Title);
        }

        return [item];
    }

    private async Task<IEnumerable<MediaItem>> CreateEpisodeMediaItem(
        string title,
        string pageUrl,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            IEnumerable<MediaItem> episodes = Enumerable.Empty<MediaItem>();

            if (!episodes.Any())
            {
                var html = await FetchHtmlAsync(pageUrl, cancellationToken);
                episodes = await _episodeExtractor.ExtractEpisodesAsync(pageUrl, title, html, Name, cancellationToken);

                if (MediaItemRepository != null && episodes.Any())
                {
                    await MediaItemRepository.SaveRangeAsync(episodes);
                }
            }

            if (episodes.Any())
            {
                Logger.LogDebug("Found {Count} episodes for series {Title}", episodes.Count(), title);
                return episodes;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting episodes from detail page, falling back to single item");
        }

        return Enumerable.Empty<MediaItem>();
    }

    private HtmlNode? FindTitleLink(HtmlNode node)
    {
        foreach (var selector in _configuration.TitleLinkSelectors)
        {
            var linkNode = node.SelectSingleNode(selector);
            if (linkNode != null)
                return linkNode;
        }

        // Fallback: try any link
        return node.SelectSingleNode(".//a") ?? (node.Name == "a" ? node : null);
    }

    private string ExtractTitle(HtmlNode node)
    {
        foreach (var selector in _configuration.TitleSelectors)
        {
            var titleNode = node.SelectSingleNode(selector);
            if (titleNode != null)
                return  titleNode.InnerText.Trim();
        }


        // Fallback: try any link
        var fallbackTitleNode = node.SelectSingleNode(".//a") ?? (node.Name == "a" ? node : null);
        var titleText = fallbackTitleNode?.InnerText.Trim();

        if (string.IsNullOrWhiteSpace(titleText))
            return string.Empty;

        // Clean title - remove HTML entities, <br> tags, and extra whitespace
        titleText = System.Net.WebUtility.HtmlDecode(titleText!);
        titleText = Regex.Replace(titleText!, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        titleText = Regex.Replace(titleText!, @"\s+", " ").Trim();

        return titleText!;
    }

    private string ExtractDetailLink(HtmlNode detailLinkNode, HtmlNode node)
    {
        var detailLink = detailLinkNode.GetAttributeValue("href", "");
        
        if (!string.IsNullOrEmpty(detailLink) && !detailLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            detailLink = new Uri(new Uri(_configuration.BaseUrl), detailLink).ToString();
        }

        if (string.IsNullOrEmpty(detailLink))
        {
            var firstLink = node.SelectSingleNode(".//a");
            if (firstLink != null)
            {
                detailLink = firstLink.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(detailLink) && !detailLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    detailLink = new Uri(new Uri(_configuration.BaseUrl), detailLink).ToString();
                }
            }
        }

        return detailLink;
    }

    private async Task<string?> ExtractDownloadLink(HtmlNode node, string detailLink, CancellationToken cancellationToken)
    {
        // Try to find link directly in the node
        var link = _linkExtractor.ExtractLink(node, _configuration.BaseUrl);
        
        if (string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(detailLink))
        {
            try
            {
                var html = await FetchHtmlAsync(detailLink, cancellationToken);
                link = _linkExtractor.ExtractLinkFromDetailPage(html, _configuration.BaseUrl);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to fetch torrent link from detail page {Url}", detailLink);
            }
        }

        return link ?? detailLink;
    }

    private string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        foreach (var pattern in _configuration.TitleCleanupPatterns)
        {
            title = Regex.Replace(title, pattern, "", RegexOptions.IgnoreCase);
        }

        return title.Trim();
    }

    private string? ExtractDateFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var yearMatch = YearRegex.Match(text);
        if (yearMatch.Success)
        {
            var year = yearMatch.Groups[1].Success ? yearMatch.Groups[1].Value : yearMatch.Groups[2].Value;
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
}
