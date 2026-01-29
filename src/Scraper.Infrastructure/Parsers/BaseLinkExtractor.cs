using HtmlAgilityPack;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of ILinkExtractor with common link extraction patterns
/// </summary>
public class BaseLinkExtractor : ILinkExtractor
{
    public virtual string? ExtractLink(HtmlNode node, string baseUrl)
    {
        // Try to find magnet link first
        var magnetLink = node.SelectSingleNode(".//a[contains(@href, 'magnet:')]")
            ?? node.SelectSingleNode("//a[contains(@href, 'magnet:')]");
        
        if (magnetLink != null)
        {
            var magnet = magnetLink.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(magnet))
                return magnet;
        }

        // Try to find torrent download link
        var torrentLink = node.SelectSingleNode(".//a[contains(@href, '.torrent')]")
            ?? node.SelectSingleNode(".//a[contains(text(), 'Download')]")
            ?? node.SelectSingleNode(".//a[contains(text(), 'Baixar')]");

        if (torrentLink != null)
        {
            var torrent = torrentLink.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(torrent))
            {
                if (!torrent.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    torrent = new Uri(new Uri(baseUrl), torrent).ToString();
                }
                return torrent;
            }
        }

        return null;
    }

    public virtual string? ExtractLinkFromDetailPage(string html, string baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Try to find magnet link first
        var magnetLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'magnet:')]");
        if (magnetLink != null)
        {
            var magnet = magnetLink.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(magnet))
                return magnet;
        }

        // Try to find torrent download link
        var torrentLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '.torrent')]")
            ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'download')]")
            ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'baixar')]")
            ?? doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Download')]")
            ?? doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Baixar')]")
            ?? doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Magnet')]");

        if (torrentLink != null)
        {
            var torrent = torrentLink.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(torrent))
            {
                if (!torrent.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    torrent = new Uri(new Uri(baseUrl), torrent).ToString();
                }
                return torrent;
            }
        }

        return null;
    }
}
