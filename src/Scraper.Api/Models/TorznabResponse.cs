using System.Xml.Serialization;

namespace Scraper.Api.Models;

[XmlRoot("rss")]
public class TorznabRss
{
    [XmlAttribute("version")]
    public string Version { get; set; } = "2.0";

    [XmlElement("channel")]
    public TorznabChannel Channel { get; set; } = new();
}

public class TorznabChannel
{
    [XmlElement("title")]
    public string Title { get; set; } = "Media Scraper";

    [XmlElement("description")]
    public string Description { get; set; } = "Torznab-compatible media scraper";

    [XmlElement("link")]
    public string Link { get; set; } = string.Empty;

    [XmlElement("language")]
    public string Language { get; set; } = "en-us";

    [XmlElement("category")]
    public string Category { get; set; } = string.Empty;

    [XmlElement("item")]
    public List<TorznabItem> Items { get; set; } = new();
}

public class TorznabItem
{
    [XmlElement("title")]
    public string Title { get; set; } = string.Empty;

    [XmlElement("guid")]
    public TorznabGuid Guid { get; set; } = new();

    [XmlElement("link")]
    public string Link { get; set; } = string.Empty;

    [XmlElement("pubDate")]
    public string PubDate { get; set; } = string.Empty;

    [XmlElement("description")]
    public string Description { get; set; } = string.Empty;

    [XmlElement("category")]
    public List<string> Categories { get; set; } = new();

    [XmlElement("enclosure")]
    public TorznabEnclosure? Enclosure { get; set; }

    [XmlElement("torznab:attr", Namespace = "http://torznab.com/schemas/2015/feed")]
    public List<TorznabAttribute> Attributes { get; set; } = new();

    [XmlElement("size")]
    public long Size { get; set; }
}

public class TorznabGuid
{
    [XmlAttribute("isPermaLink")]
    public bool IsPermaLink { get; set; } = false;

    [XmlText]
    public string Value { get; set; } = string.Empty;
}

public class TorznabEnclosure
{
    [XmlAttribute("url")]
    public string Url { get; set; } = string.Empty;

    [XmlAttribute("length")]
    public long Length { get; set; }

    [XmlAttribute("type")]
    public string Type { get; set; } = "application/x-bittorrent";
}

public class TorznabAttribute
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("value")]
    public string Value { get; set; } = string.Empty;
}

