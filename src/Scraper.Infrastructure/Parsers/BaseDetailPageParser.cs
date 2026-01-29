using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;
using System.Globalization;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of IDetailPageParser
/// </summary>
public class BaseDetailPageParser : IDetailPageParser
{
    private readonly ILogger Logger;
    private readonly ScraperConfiguration Configuration;
    private readonly IMetadataExtractor MetadataExtractor;

    public BaseDetailPageParser(
        ILogger logger,
        ScraperConfiguration configuration,
        IMetadataExtractor metadataExtractor
    )
    {
        Logger = logger;
        Configuration = configuration;
        MetadataExtractor = metadataExtractor;
    }

    public void EnrichMediaItem(MediaItem item, string detailUrl, string html, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Enriching item from detail page: {Url}", detailUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var infoSection = GetInfoSection(doc);
            var infoBlock = ExtractInfoBlock(infoSection);

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

        if (double.TryParse(normalized,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var value))
        {
            return (long)(value * multiplier);
        }

        return 0;
    }

    public virtual DateTime ParseDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return DateTime.UtcNow;

        // Try common date formats
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

        // Fallback to TryParse
        if (DateTime.TryParse(dateText, out var parsedDate))
        {
            return parsedDate;
        }

        return DateTime.UtcNow;
    }

    public virtual HtmlNode? GetInfoSection(HtmlDocument doc)
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

        foreach (var strong in strongNodes)
        {
            var key = strong.InnerText
                .Replace(":", "")
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
}
