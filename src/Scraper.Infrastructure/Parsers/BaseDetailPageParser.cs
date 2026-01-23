using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;
using System.Globalization;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of IDetailPageParser
/// </summary>
public abstract class BaseDetailPageParser : IDetailPageParser
{
    protected readonly ILogger Logger;
    protected readonly IMetadataExtractor MetadataExtractor;
    protected readonly ScraperConfiguration Configuration;

    protected BaseDetailPageParser(
        ILogger logger,
        IMetadataExtractor metadataExtractor,
        ScraperConfiguration configuration)
    {
        Logger = logger;
        MetadataExtractor = metadataExtractor;
        Configuration = configuration;
    }

    public abstract Task EnrichMediaItemAsync(MediaItem item, string detailUrl, string html, CancellationToken cancellationToken = default);

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

    protected virtual HtmlNode? GetInfoSection(HtmlDocument doc)
    {
        foreach (var selector in Configuration.InfoSectionSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
                return node;
        }
        return doc.DocumentNode;
    }

    protected Dictionary<string, string> ExtractInfoBlock(HtmlNode pNode)
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
