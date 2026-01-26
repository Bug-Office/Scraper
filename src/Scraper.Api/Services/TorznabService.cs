using Scraper.Api.Models;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

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

    public async Task<TorznabRss> SearchAsync(
        string query, 
        string? imdbId = null, 
        MediaType? type = null, 
        string? baseUrl = null,
        string? offset = null,
        string? limit = null,
        string? skipDatabase = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest
        {
            Query = query,
            ImdbId = imdbId,
            Type = type
        };

        if (!string.IsNullOrEmpty(offset))
            request.Offset = int.Parse(offset);

        if (!string.IsNullOrEmpty(limit))
            request.Limit = int.Parse(limit);

        if (!string.IsNullOrEmpty(skipDatabase))
            request.SkipDatabase = bool.Parse(skipDatabase);

        var items = await _scraperService.SearchAsync(request, cancellationToken);

        var rss = new TorznabRss
        {
            Channel = new TorznabChannel
            {
                Title = "Media Scraper",
                Description = "Torznab-compatible media scraper",
                Link = string.Empty,
                Language = "en-us",
                Items = items.Select(item => ConvertToTorznabItem(item, baseUrl, apiKey)).ToList()
            }
        };

        return rss;
    }

    //private TorznabItem ConvertToTorznabItem(MediaItem item, string? baseUrl = null, string? apiKey = null)
    //{
    //    var magnet = item.MagnetLink;
    //    var fileSize = item.FileSize > 0
    //        ? item.FileSize
    //        : 15_000_000_000; // fallback seguro (~15GB)

    //    var torznabItem = new TorznabItem
    //    {
    //        Title = _titleNormalizer.GenerateSceneReleaseName(item),

    //        Guid = new TorznabGuid
    //        {
    //            Value = item.Title
    //        },

    //        Link = magnet,

    //        PubDate = item.PublishDate.ToUniversalTime()
    //            .ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture),

    //        Description = item.Description ?? item.Title,

    //        Size = fileSize,

    //        Categories = GetCategories(item.Type, item.Resolution),

    //        Attributes = GetAttributes(item),

    //        Enclosure = new TorznabEnclosure
    //        {
    //            Url = magnet,
    //            Length = fileSize,
    //            Type = "application/x-bittorrent"
    //        }
    //    };

    //    return torznabItem;
    //}

    private TorznabItem ConvertToTorznabItem(MediaItem item, string? baseUrl = null, string? apiKey = null)
    {
        var magnet = item.MagnetLink;

        var fileSize = item.FileSize > 0
            ? item.FileSize
            : 4_000_000_000; // fallback seguro

        var torznabItem = new TorznabItem
        {
            // precisa ser release name estilo scene
            Title = _titleNormalizer.GenerateSafeReleaseName(item),

            Guid = new TorznabGuid
            {
                //IsPermaLink = false,
                Value = magnet
            },

            Link = magnet,

            Comments = item.PageUrl,

            PubDate = item.PublishDate.ToUniversalTime()
                .ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture),

            Description = item.Description ?? item.Title,

            Size = fileSize,

            Categories = new() { "2000" },

            Enclosure = new TorznabEnclosure
            {
                Url = magnet,
                Length = fileSize,
                Type = "application/x-bittorrent"
            },

            Attributes = new List<TorznabAttribute>
            {
                new() { Name = "indexer", Value = item.Scraper },
                new() { Name = "source", Value = "torrent" },
                new() { Name = "imdbId", Value = item.ImdbId ?? "" },
                new() { Name = "tmdbId", Value = item.TmdbId ?? "" },
                new() { Name = "category", Value = "2000" },
                new() { Name = "tag", Value = item.Scraper },
                new() { Name = "genre", Value = "" },
                new() { Name = "seeders", Value = "1" },
                new() { Name = "grabs", Value = "1" },
                new() { Name = "peers", Value = "1" },
                new() { Name = "downloadvolumefactor", Value = "0" },
                new() { Name = "uploadvolumefactor", Value = "1" },
            }
        };

        return torznabItem;
    }

    private static string ExtractBtih(string magnet)
    {
        var match = Regex.Match(magnet, @"btih:([a-fA-F0-9]+)");
        return match.Success
            ? $"btih-{match.Groups[1].Value.ToLowerInvariant()}"
            : Guid.NewGuid().ToString(); // fallback raro
    }




    private List<string> GetCategories(MediaType type, string? resolution = null)
    {
        var categories = new List<string>();
        
        // Base category based on media type
        if (type == MediaType.Movie)
        {
            categories.Add("2000"); // Movies base category
            
            // Add subcategories based on resolution
            if (!string.IsNullOrEmpty(resolution))
            {
                var res = resolution.ToLowerInvariant();
                if (res.Contains("2160") || res.Contains("4k") || res.Contains("uhd"))
                {
                    categories.Add("2045"); // Movies/UHD
                }
                else if (res.Contains("1080") || res.Contains("720"))
                {
                    categories.Add("2040"); // Movies/HD
                }
                else
                {
                    categories.Add("2030"); // Movies/SD
                }
            }
            else
            {
                // Default to HD if resolution unknown
                categories.Add("2040"); // Movies/HD
            }
        }
        else if (type == MediaType.TvShow)
        {
            categories.Add("5000"); // TV base category
            
            // Add subcategories based on resolution
            if (!string.IsNullOrEmpty(resolution))
            {
                var res = resolution.ToLowerInvariant();
                if (res.Contains("2160") || res.Contains("4k") || res.Contains("uhd"))
                {
                    categories.Add("5045"); // TV/UHD
                }
                else if (res.Contains("1080") || res.Contains("720"))
                {
                    categories.Add("5040"); // TV/HD
                }
                else
                {
                    categories.Add("5030"); // TV/SD
                }
            }
            else
            {
                // Default to HD if resolution unknown
                categories.Add("5040"); // TV/HD
            }
        }
        else
        {
            // Unknown type - include both base categories
            categories.Add("2000"); // Movies
            categories.Add("5000"); // TV
        }
        
        return categories;
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

        // Languages
        if (item.Languages != null && item.Languages.Any())
        {
            var languageValues = item.Languages
                .Select(lang => lang switch
                {
                    MediaLanguage.Portuguese => "porguese",
                    MediaLanguage.English => "english",
                    MediaLanguage.Japanese => "japanese",
                    _ => null
                })
                .Where(v => v != null)
                .Distinct()
                .ToList();

            foreach (var language in languageValues) {
                attributes.Add(new TorznabAttribute { Name = "language", Value = language });
            }
        }

        // IMDB ID if available
        if (!string.IsNullOrEmpty(item.ImdbId))
        {
            attributes.Add(new TorznabAttribute { Name = "imdbid", Value = item.ImdbId });
        }

        attributes.Add(new TorznabAttribute { Name = "seeders", Value = item.Seeders?.ToString() ?? "1" });
        
        attributes.Add(new TorznabAttribute { Name = "peers", Value = item.Leechers?.ToString() ?? "1" });
       

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

