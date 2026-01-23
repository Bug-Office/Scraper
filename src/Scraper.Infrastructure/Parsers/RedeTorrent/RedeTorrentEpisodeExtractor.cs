using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Interfaces;
using System.Text.RegularExpressions;

namespace Scraper.Infrastructure.Parsers.RedeTorrent;

/// <summary>
/// Episode extractor specific to Rede Torrent website
/// </summary>
public class RedeTorrentEpisodeExtractor : BaseEpisodeExtractor
{
    public RedeTorrentEpisodeExtractor(
        ILogger<RedeTorrentEpisodeExtractor> logger,
        ITitleNormalizer titleNormalizer,
        ITmdbService tmdbService,
        IMetadataExtractor metadataExtractor,
        ILinkExtractor linkExtractor)
        : base(logger, titleNormalizer, tmdbService, metadataExtractor, linkExtractor)
    {
    }

    public override async Task<IEnumerable<MediaItem>> ExtractEpisodesAsync(
        string detailUrl, 
        string seriesTitle, 
        string html, 
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

                    var episodeText = paragraph.InnerText;
                    
                    // Extract episode number
                    var episodeNumber = ExtractEpisodeNumber(episodeText);

                    // Try to extract from magnet link
                    string? seasonNumber = null;
                    if (string.IsNullOrEmpty(episodeNumber))
                    {
                        var (_season, _episode) = ExtractSeasonEpisodeFromMagnet(magnet);
                        seasonNumber = _season;
                        episodeNumber = _episode;
                    }

                    // If still no episode number, try alternative patterns
                    if (string.IsNullOrEmpty(episodeNumber))
                    {
                        var altMatch = Regex.Match(episodeText, @"(\d{1,2})[º°]?\s*(?:EPIS[ÓO]DIO|EP)", RegexOptions.IgnoreCase);
                        if (altMatch.Success)
                        {
                            episodeNumber = altMatch.Groups[1].Value;
                        }
                    }

                    // Extract season number from series title if not found
                    if (string.IsNullOrEmpty(seasonNumber))
                    {
                        seasonNumber = ExtractSeasonFromTitle(seriesTitle);
                    }

                    // Build episode title
                    var episodeTitle = BuildEpisodeTitle(seriesTitle, seasonNumber, episodeNumber);

                    if (string.IsNullOrEmpty(episodeNumber))
                    {
                        Logger.LogWarning("Could not determine episode number for magnet link in {Url}", detailUrl);
                    }

                    // Extract metadata
                    var dateText = ExtractDateFromText(doc.DocumentNode.InnerText);
                    var sizeText = MetadataExtractor.ExtractSize(episodeText);

                    var episode = CreateEpisodeItem(
                        episodeTitle,
                        detailUrl,
                        magnet,
                        sizeText,
                        dateText,
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

    private string? ExtractDateFromText(string text)
    {
        // Look for year in "Filme de YYYY" or "Série de YYYY"
        var yearMatch = Regex.Match(text, @"\(Filme de (\d{4})\)|\(Série de (\d{4})\)", RegexOptions.Compiled);
        if (yearMatch.Success)
        {
            var year = yearMatch.Groups[1].Success ? yearMatch.Groups[1].Value : yearMatch.Groups[2].Value;
            return $"{year}-01-01";
        }
        return null;
    }
}
