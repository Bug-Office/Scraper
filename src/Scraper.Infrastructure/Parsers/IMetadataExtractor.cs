using Scraper.Core.Models;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Interface for extracting metadata from text/HTML
/// </summary>
public interface IMetadataExtractor
{
    string? ExtractSize(string? text);
    string? ExtractFormat(string? text);
    string? ExtractQuality(string? text);
    List<MediaLanguage> ExtractLanguages(string? text);
    string? ExtractEpisodeNumber(string text);
    (string? Season, string? Episode) ExtractSeasonEpisodeFromMagnet(string magnet);
    string? ExtractDateFromText(string text);
    string ExtractSeasonFromTitle(string seriesTitle);
}
