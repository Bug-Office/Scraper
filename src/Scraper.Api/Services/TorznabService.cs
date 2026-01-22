using System.Globalization;
using System.Xml.Serialization;
using Scraper.Api.Models;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Api.Services;

public class TorznabService
{
    private readonly IScraperService _scraperService;
    private readonly ITitleNormalizer _titleNormalizer;

    public TorznabService(IScraperService scraperService, ITitleNormalizer titleNormalizer)
    {
        _scraperService = scraperService;
        _titleNormalizer = titleNormalizer;
    }

    public async Task<TorznabRss> SearchAsync(string query, string? imdbId = null, MediaType? type = null, CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest
        {
            Query = query,
            ImdbId = imdbId,
            Type = type
        };

        var items = await _scraperService.SearchAsync(request, cancellationToken);

        var rss = new TorznabRss
        {
            Channel = new TorznabChannel
            {
                Title = "Media Scraper",
                Description = "Torznab-compatible media scraper",
                Link = string.Empty,
                Language = "en-us",
                Items = items.Select(item => ConvertToTorznabItem(item)).ToList()
            }
        };

        return rss;
    }

    private TorznabItem ConvertToTorznabItem(MediaItem item)
    {
        var torznabItem = new TorznabItem
        {
            Title = _titleNormalizer.GenerateSafeReleaseName(item),
            Guid = new TorznabGuid
            {
                IsPermaLink = false,
                Value = item.Guid
            },
            Link = !string.IsNullOrEmpty(item.MagnetLink) ? item.MagnetLink : item.TorrentLink,
            PubDate = item.PublishDate.ToString("ddd, dd MMM yyyy HH:mm:ss UTC", CultureInfo.InvariantCulture),
            Description = item.Description ?? item.Title,
            Size = item.FileSize,
            Categories = GetCategories(item.Type),
            Attributes = GetAttributes(item)
        };

        // Set enclosure if torrent link is available
        if (!string.IsNullOrEmpty(item.TorrentLink))
        {
            torznabItem.Enclosure = new TorznabEnclosure
            {
                Url = item.TorrentLink,
                Length = item.FileSize,
                Type = "application/x-bittorrent"
            };
        }

        return torznabItem;
    }

    private List<string> GetCategories(MediaType type)
    {
        return type switch
        {
            MediaType.Movie => new List<string> { "2000" }, // Movies
            MediaType.TvShow => new List<string> { "5000" }, // TV
            _ => new List<string> { "2000", "5000" } // Both
        };
    }

    private List<TorznabAttribute> GetAttributes(MediaItem item)
    {
        var attributes = new List<TorznabAttribute>();

        // Resolution
        if (!string.IsNullOrEmpty(item.Resolution))
        {
            var resolutionValue = item.Resolution.Replace("p", "");
            attributes.Add(new TorznabAttribute { Name = "resolution", Value = resolutionValue });
        }

        // Language
        if (item.Language != MediaLanguage.Unknown)
        {
            var languageValue = item.Language switch
            {
                MediaLanguage.PtBr => "pt-br",
                MediaLanguage.Dual => "dual",
                MediaLanguage.Legendado => "legendado",
                _ => "unknown"
            };
            attributes.Add(new TorznabAttribute { Name = "language", Value = languageValue });
        }

        // IMDB ID if available
        if (!string.IsNullOrEmpty(item.ImdbId))
        {
            attributes.Add(new TorznabAttribute { Name = "imdbid", Value = item.ImdbId });
        }

        // Seeders/Leechers if available
        if (item.Seeders.HasValue)
        {
            attributes.Add(new TorznabAttribute { Name = "seeders", Value = item.Seeders.Value.ToString() });
        }

        if (item.Leechers.HasValue)
        {
            attributes.Add(new TorznabAttribute { Name = "peers", Value = item.Leechers.Value.ToString() });
        }

        return attributes;
    }

    public string SerializeToXml(TorznabRss rss)
    {
        var serializer = new XmlSerializer(typeof(TorznabRss));
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add("torznab", "http://torznab.com/schemas/2015/feed");
        namespaces.Add("", ""); // Remove default namespace

        var settings = new System.Xml.XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = System.Text.Encoding.UTF8
        };

        using var stringWriter = new System.IO.StringWriter();
        using var xmlWriter = System.Xml.XmlWriter.Create(stringWriter, settings);
        serializer.Serialize(xmlWriter, rss, namespaces);
        
        var xml = stringWriter.ToString();
        
        // Manually add xmlns:torznab attribute to the rss root element
        // This is needed because XmlSerializer doesn't support xmlns attributes directly
        if (xml.Contains("<rss"))
        {
            xml = xml.Replace(
                "<rss version=\"2.0\">",
                "<rss version=\"2.0\" xmlns:torznab=\"http://torznab.com/schemas/2015/feed\">"
            );
        }
        
        return xml;
    }
}

