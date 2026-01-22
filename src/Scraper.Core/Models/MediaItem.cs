namespace Scraper.Core.Models;

public class MediaItem
{
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public string MagnetLink { get; set; } = string.Empty;
    public string TorrentLink { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime PublishDate { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public MediaLanguage Language { get; set; }
    public MediaType Type { get; set; }
    public string? ImdbId { get; set; }
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public string? Description { get; set; }
    public int? Seeders { get; set; }
    public int? Leechers { get; set; }
}

public enum MediaLanguage
{
    Unknown,
    PtBr,
    Dual,
    Legendado
}

public enum MediaType
{
    Movie,
    TvShow,
    Unknown
}

