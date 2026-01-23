using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Data.Entities;

[Table("MediaItems")]
public class MediaItemEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string NormalizedTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string PageUrl { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string MagnetLink { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string TorrentLink { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime PublishDate { get; set; }

    [MaxLength(50)]
    public string Resolution { get; set; } = string.Empty;

    public int Language { get; set; } // Enum como int

    public int Type { get; set; } // Enum como int

    [MaxLength(50)]
    public string? ImdbId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? Seeders { get; set; }

    public int? Leechers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Método para converter de MediaItem para Entity
    public static MediaItemEntity FromMediaItem(MediaItem item)
    {
        return new MediaItemEntity
        {
            Title = item.Title,
            NormalizedTitle = item.NormalizedTitle,
            PageUrl = item.PageUrl,
            MagnetLink = item.MagnetLink,
            TorrentLink = item.TorrentLink,
            FileSize = item.FileSize,
            PublishDate = item.PublishDate,
            Resolution = item.Resolution,
            Language = (int)item.Language,
            Type = (int)item.Type,
            ImdbId = item.ImdbId,
            Guid = item.Guid,
            Description = item.Description,
            Seeders = item.Seeders,
            Leechers = item.Leechers,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Método para converter de Entity para MediaItem
    public MediaItem ToMediaItem()
    {
        return new MediaItem
        {
            Title = Title,
            NormalizedTitle = NormalizedTitle,
            PageUrl = PageUrl,
            MagnetLink = MagnetLink,
            TorrentLink = TorrentLink,
            FileSize = FileSize,
            PublishDate = PublishDate,
            Resolution = Resolution,
            Language = (MediaLanguage)Language,
            Type = (MediaType)Type,
            ImdbId = ImdbId,
            Guid = Guid,
            Description = Description,
            Seeders = Seeders,
            Leechers = Leechers
        };
    }
}

