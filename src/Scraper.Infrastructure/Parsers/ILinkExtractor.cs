using HtmlAgilityPack;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Interface for extracting download links (magnet/torrent) from HTML
/// </summary>
public interface ILinkExtractor
{
    /// <summary>
    /// Extracts magnet or torrent link from HTML node
    /// </summary>
    string? ExtractLink(HtmlNode node, string baseUrl);
    
    /// <summary>
    /// Extracts magnet or torrent link from detail page HTML
    /// </summary>
    string? ExtractLinkFromDetailPage(string html, string baseUrl);
}
