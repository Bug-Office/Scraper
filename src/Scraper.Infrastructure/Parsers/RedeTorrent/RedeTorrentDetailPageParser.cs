using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Models;
using Scraper.Infrastructure.Configurations;

namespace Scraper.Infrastructure.Parsers.RedeTorrent;

/// <summary>
/// Detail page parser specific to Rede Torrent website
/// </summary>
public class RedeTorrentDetailPageParser : BaseDetailPageParser
{
    public RedeTorrentDetailPageParser(
        ILogger<RedeTorrentDetailPageParser> logger,
        IMetadataExtractor metadataExtractor,
        ScraperConfiguration configuration)
        : base(logger, metadataExtractor, configuration)
    {
    }

    public override async Task EnrichMediaItemAsync(MediaItem item, string detailUrl, string html, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Enriching item from detail page: {Url}", detailUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var infoSection = GetInfoSection(doc);
            var infoText = infoSection?.InnerText ?? string.Empty;

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
}
