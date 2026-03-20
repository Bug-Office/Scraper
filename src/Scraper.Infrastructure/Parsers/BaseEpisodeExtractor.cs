using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of IEpisodeExtractor with common episode extraction patterns
/// </summary>
public class BaseEpisodeExtractor : IEpisodeExtractor
{
    private readonly ILogger Logger;
    private readonly ScraperConfiguration Configuration;
    private readonly ITmdbService TmdbService;
    private readonly IMetadataExtractor MetadataExtractor;
    private readonly ITitleNormalizer TitleNormalizer;


    public BaseEpisodeExtractor(
        ILogger logger,
        ScraperConfiguration configuration,
        IMetadataExtractor metadataExtractor,
        ITmdbService tmdbService,
        ITitleNormalizer titleNormalizer
    )
    {
        Logger = logger;
        Configuration = configuration;
        MetadataExtractor = metadataExtractor;
        TmdbService = tmdbService;
        TitleNormalizer = titleNormalizer;
    }

    public async Task<IEnumerable<MediaItem>> ExtractEpisodesAsync(
            string detailUrl,
            string seriesTitle,
            string html,
            string scrapperName,
            CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Extracting episodes from detail page: {Url}", detailUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var episodes = new List<MediaItem>();
            var downloadSection = GetDownloadSection(doc);
            var episodeParagraphs = GetEpisodeParagraphs(downloadSection);

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

                    var episodeText = paragraph.FirstChild.InnerText;

                    // Extract season/episode number from magnet link
                    var (seasonNumber, episodeNumber) = MetadataExtractor.ExtractSeasonEpisodeFromMagnet(magnet);

                    // Extract season number from series title if not found
                    if (string.IsNullOrEmpty(seasonNumber))
                    {
                        seasonNumber = MetadataExtractor.ExtractSeasonFromTitle(seriesTitle);
                    }

                    // Extract episode number from series title if not found
                    if (string.IsNullOrEmpty(seasonNumber))
                    {
                        episodeNumber = MetadataExtractor.ExtractEpisodeNumber(episodeText);
                    }

                    // Build episode title
                    var episodeTitle = BuildEpisodeTitle(seriesTitle, seasonNumber, episodeNumber);

                    if (string.IsNullOrEmpty(episodeNumber))
                    {
                        Logger.LogWarning("Could not determine episode number for magnet link in {Url}", detailUrl);
                    }

                    // Extract metadata
                    var dateText = MetadataExtractor.ExtractDateFromText(doc.DocumentNode.InnerText);
                    var sizeText = MetadataExtractor.ExtractSize(episodeText);

                    var episode = CreateEpisodeItem(
                        episodeTitle,
                        detailUrl,
                        magnet,
                        sizeText,
                        dateText,
                        MediaType.TvShow,
                        scrapperName
                    );

                    EnrichMediaItemAsync(episode, detailUrl, html, cancellationToken);

                    NormalizeMediaItem(episode);

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

    protected virtual void NormalizeMediaItem(MediaItem item)
    {
        item.NormalizedTitle = TitleNormalizer.NormalizeTitle(item);

        var tmdbmovieDetails = TmdbService.GetTmdbDetailsByTitleAsync(item.Title, item.ReleaseDate.Year, item.Type).GetAwaiter().GetResult();

        item.Title = tmdbmovieDetails?.Title ?? item.Title;
        item.NormalizedTitle = tmdbmovieDetails?.Title ?? item.NormalizedTitle;
        item.ReleaseDate = tmdbmovieDetails?.ReleaseDate ?? item.ReleaseDate;
        item.ImdbId = tmdbmovieDetails?.ImdbId.Split("tt").ElementAt(1);
        item.TmdbId = tmdbmovieDetails?.Id.ToString();
    }

    public virtual MediaItem CreateEpisodeItem(
        string episodeTitle,
        string detailUrl,
        string link,
        string? sizeText,
        string? dateText,
        MediaType type,
        string srapper)
    {
        var normalizedTitle = TitleNormalizer.NormalizeTitle(episodeTitle);
        var languages = TitleNormalizer.DetectLanguages(episodeTitle);
        var resolution = TitleNormalizer.DetectResolution(episodeTitle);

        var tmdbDetails = TmdbService.GetTmdbDetailsByTitleAsync(normalizedTitle, null).GetAwaiter().GetResult();

        var item = new MediaItem
        {
            Title = episodeTitle,
            PageUrl = detailUrl,
            NormalizedTitle = normalizedTitle,
            Languages = languages,
            Resolution = resolution,
            Type = type,
            ImdbId = tmdbDetails?.ImdbId,
            ReleaseDate = tmdbDetails?.ReleaseDate ?? DateTime.UtcNow,
            Guid = link,
            Scraper = srapper
        };

        if (link.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            item.MagnetLink = link;
        }
        else
        {
            item.TorrentLink = link;
        }

        // Parse size if available
        if (!string.IsNullOrEmpty(sizeText))
        {
            var size = ParseFileSize(sizeText);
            if (size > 0)
            {
                item.FileSize = size;
            }
        }

        return item;
    }

    public virtual HtmlNode? GetDownloadSection(HtmlDocument doc)
    {
        foreach (var selector in Configuration.DownloadSectionSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
                return node;
        }
        return doc.DocumentNode;
    }

    public virtual IEnumerable<HtmlNode> GetEpisodeParagraphs(HtmlNode downloadSection)
    {
        foreach (var selector in Configuration.EpisodeParagraphSelectors)
        {
            var nodes = downloadSection.SelectNodes(selector);
            if (nodes != null && nodes.Count > 0)
                return nodes;
        }
        return Enumerable.Empty<HtmlNode>();
    }

    

    public virtual string BuildEpisodeTitle(string seriesTitle, string? seasonNumber, string? episodeNumber)
    {
        var cleanSeriesTitle = Regex.Replace(seriesTitle, @"\s*\d+[ªa]\s*Temporada\s*", " ", RegexOptions.IgnoreCase).Trim();
        cleanSeriesTitle = Regex.Replace(cleanSeriesTitle, @"\s*Torrent\s*Download\s*$", "", RegexOptions.IgnoreCase).Trim();
        
        if (!string.IsNullOrEmpty(episodeNumber))
        {
            return $"{cleanSeriesTitle} S{seasonNumber?.PadLeft(2, '0') ?? "01"}E{episodeNumber.PadLeft(2, '0')}";
        }

        if (!string.IsNullOrEmpty(seasonNumber))
        {
            return $"{cleanSeriesTitle} S{seasonNumber?.PadLeft(2, '0') ?? "01"}";
        }

        return $"{cleanSeriesTitle} Episode";
    }

    public virtual long ParseFileSize(string? sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
            return 0;

        var normalized = sizeText.Trim().ToUpperInvariant();
        var multiplier = 1L;

        if (normalized.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L * 1024L;
            normalized = normalized.Replace("GB", "").Trim();
        }
        else if (normalized.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L;
            normalized = normalized.Replace("MB", "").Trim();
        }
        else if (normalized.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L;
            normalized = normalized.Replace("KB", "").Trim();
        }

        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return (long)(value * multiplier);
        }

        return 0;
    }

    public virtual DateTime ParseDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return DateTime.UtcNow;

        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy",
            "dd.MM.yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateText, format, null, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        if (DateTime.TryParse(dateText, out var parsedDate))
        {
            return parsedDate;
        }

        return DateTime.UtcNow;
    }

    private HtmlNode? GetInfoSection(HtmlDocument doc)
    {
        foreach (var selector in Configuration.InfoSectionSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
                return node;
        }
        return doc.DocumentNode;
    }

    private Dictionary<string, string> ExtractInfoBlock(HtmlNode pNode)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var strongNodes = pNode.SelectNodes(".//strong");
        if (strongNodes == null)
            return result;

        result["Título"] = strongNodes[1].InnerHtml.Trim();

        foreach (var strong in strongNodes)
        {
            var key = strong.InnerText
                .Replace(":", "")
                .Replace(",", "")
                .Trim();

            var valueNode = strong
                .SelectSingleNode("following-sibling::text()[1]");

            if (valueNode == null)
                continue;

            var value = HtmlEntity.DeEntitize(valueNode.InnerText).Trim();

            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    public void EnrichMediaItemAsync(MediaItem item, string detailUrl, string html, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Enriching item from detail page: {Url}", detailUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var infoSection = GetInfoSection(doc);
            var infoBlock = ExtractInfoBlock(infoSection);

            // Extract size
            var titleText = TitleNormalizer.NormalizeTitle(infoBlock.GetValueOrDefault("Título"));
            if (!string.IsNullOrEmpty(titleText))
            {
                item.Title = titleText;
            }

            // Extract size
            var sizeText = MetadataExtractor.ExtractSize(infoBlock.GetValueOrDefault("Tamanho"));
            if (!string.IsNullOrEmpty(sizeText))
            {
                var size = ParseFileSize(sizeText);
                if (size > 0)
                {
                    item.FileSize = size;
                }
            }

            // Extract format
            var format = MetadataExtractor.ExtractFormat(infoBlock.GetValueOrDefault("Formato"));
            if (!string.IsNullOrEmpty(format))
            {
                item.Format = format;
            }

            // Extract quality/resolution
            var quality = MetadataExtractor.ExtractQuality(infoBlock.GetValueOrDefault("Qualidade"));
            if (!string.IsNullOrEmpty(quality))
            {
                item.Resolution = quality;
            }

            // Extract languages
            var languages = MetadataExtractor.ExtractLanguages(infoBlock.GetValueOrDefault("Idioma"));
            if (languages.Any())
            {
                item.Languages = languages;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error enriching item from detail page {Url}", detailUrl);
        }
    }
}
