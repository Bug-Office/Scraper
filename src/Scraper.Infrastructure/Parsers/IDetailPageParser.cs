using Scraper.Core.Models;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Interface for parsing detail pages to enrich MediaItem with additional information
/// </summary>
public interface IDetailPageParser
{
    /// <summary>
    /// Enriches a MediaItem with information extracted from the detail page HTML
    /// </summary>
    Task EnrichMediaItemAsync(MediaItem item, string detailUrl, string html, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a file size string (e.g. "2.44 GB", "700 MB") and converts it to bytes.
    /// </summary>
    /// <param name="sizeText">Raw size text extracted from HTML.</param>
    /// <returns>File size in bytes, or 0 if parsing fails.</returns>
    long ParseFileSize(string? sizeText);

    /// <summary>
    /// Parses a date string extracted from HTML and converts it to a DateTime.
    /// </summary>
    /// <param name="dateText">Raw date text (e.g. "2023", "2023-01-01").</param>
    /// <returns>
    /// A DateTime representing the parsed date, or DateTime.MinValue if parsing fails.
    /// </returns>
    DateTime ParseDate(string? dateText);
}
