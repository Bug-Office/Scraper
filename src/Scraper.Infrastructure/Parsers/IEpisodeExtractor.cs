using Scraper.Core.Models;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Interface for extracting episodes from TV series detail pages
/// </summary>
public interface IEpisodeExtractor
{
    /// <summary>
    /// Extracts all episodes from a TV series detail page
    /// </summary>
    Task<IEnumerable<MediaItem>> ExtractEpisodesAsync(
        string detailUrl, 
        string seriesTitle, 
        string html, 
        CancellationToken cancellationToken = default);
}
