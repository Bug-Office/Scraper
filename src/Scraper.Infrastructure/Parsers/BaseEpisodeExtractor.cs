using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Interfaces;
using System.Text.RegularExpressions;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of IEpisodeExtractor with common episode extraction patterns
/// </summary>
public abstract class BaseEpisodeExtractor : IEpisodeExtractor
{
    protected readonly ILogger Logger;
    protected readonly ITitleNormalizer TitleNormalizer;
    protected readonly ITmdbService TmdbService;
    protected readonly IMetadataExtractor MetadataExtractor;
    protected readonly ILinkExtractor LinkExtractor;
    
    protected static readonly Regex EpisodeNumberPattern = new(@"(\d{1,2})[º°]\s*EPIS[ÓO]DIO", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    protected static readonly Regex SeasonEpisodePattern = new(@"S(\d{1,2})E(\d{1,2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    protected static readonly Regex SeasonPattern = new(@"(\d{1,2})[ªa]\s*Temporada", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    protected BaseEpisodeExtractor(
        ILogger logger,
        ITitleNormalizer titleNormalizer,
        ITmdbService tmdbService,
        IMetadataExtractor metadataExtractor,
        ILinkExtractor linkExtractor)
    {
        Logger = logger;
        TitleNormalizer = titleNormalizer;
        TmdbService = tmdbService;
        MetadataExtractor = metadataExtractor;
        LinkExtractor = linkExtractor;
    }

    public abstract Task<IEnumerable<MediaItem>> ExtractEpisodesAsync(
        string detailUrl, 
        string seriesTitle, 
        string html, 
        CancellationToken cancellationToken = default);

    protected virtual HtmlNode? GetDownloadSection(HtmlDocument doc)
    {
        return doc.DocumentNode.SelectSingleNode("//div[@id='download']")
            ?? doc.DocumentNode.SelectSingleNode("//div[@id='lista_links']")
            ?? doc.DocumentNode;
    }

    protected virtual IEnumerable<HtmlNode> GetEpisodeParagraphs(HtmlNode downloadSection)
    {
        return downloadSection.SelectNodes(".//p[@class='text-center']")
            ?? downloadSection.SelectNodes(".//p[contains(., 'EPISÓDIO')]")
            ?? Enumerable.Empty<HtmlNode>();
    }

    protected virtual string? ExtractEpisodeNumber(string text)
    {
        var match = EpisodeNumberPattern.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    protected virtual (string? Season, string? Episode) ExtractSeasonEpisodeFromMagnet(string magnet)
    {
        var match = SeasonEpisodePattern.Match(magnet);
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }
        return (null, null);
    }

    protected virtual string ExtractSeasonFromTitle(string seriesTitle)
    {
        var match = SeasonPattern.Match(seriesTitle);
        return match.Success ? match.Groups[1].Value : "1";
    }

    protected virtual string BuildEpisodeTitle(string seriesTitle, string? seasonNumber, string? episodeNumber)
    {
        var cleanSeriesTitle = Regex.Replace(seriesTitle, @"\s*\d+[ªa]\s*Temporada\s*", " ", RegexOptions.IgnoreCase).Trim();
        cleanSeriesTitle = Regex.Replace(cleanSeriesTitle, @"\s*Torrent\s*Download\s*$", "", RegexOptions.IgnoreCase).Trim();
        
        if (!string.IsNullOrEmpty(episodeNumber))
        {
            return $"{cleanSeriesTitle} S{seasonNumber?.PadLeft(2, '0') ?? "01"}E{episodeNumber.PadLeft(2, '0')}";
        }
        
        return $"{cleanSeriesTitle} Episode";
    }

    protected virtual MediaItem CreateEpisodeItem(
        string episodeTitle,
        string detailUrl,
        string link,
        string? sizeText,
        string? dateText,
        MediaType type)
    {
        var normalizedTitle = TitleNormalizer.NormalizeTitle(episodeTitle, type);
        var languages = TitleNormalizer.DetectLanguages(episodeTitle);
        var resolution = TitleNormalizer.DetectResolution(episodeTitle);

        var tmdbDetails = TmdbService.GetTmdbMovieDetailsByTitleAsync(normalizedTitle, null).GetAwaiter().GetResult();

        var item = new MediaItem
        {
            Title = episodeTitle,
            PageUrl = detailUrl,
            NormalizedTitle = normalizedTitle,
            Languages = languages,
            Resolution = resolution,
            Type = type,
            ImdbId = tmdbDetails?.ImdbId,
            PublishDate = tmdbDetails?.ReleaseDate ?? DateTime.UtcNow,
            Guid = Guid.NewGuid().ToString()
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

    protected virtual long ParseFileSize(string? sizeText)
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

        if (double.TryParse(normalized, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return (long)(value * multiplier);
        }

        return 0;
    }

    protected virtual DateTime ParseDate(string? dateText)
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
            if (DateTime.TryParseExact(dateText, format, null, System.Globalization.DateTimeStyles.None, out var date))
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
}
