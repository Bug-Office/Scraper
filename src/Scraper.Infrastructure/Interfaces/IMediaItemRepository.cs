using Scraper.Core.Models;

namespace Scraper.Infrastructure.Interfaces;

public interface IMediaItemRepository
{
    Task<MediaItem?> GetByGuidAsync(string guid);
    Task<IEnumerable<MediaItem>> GetByQueryAsync(string query, int? limit = null);
    Task<IEnumerable<MediaItem>> GetByPageUrlAsync(string pageUrl, int? limit = null);
    Task<IEnumerable<MediaItem>> GetByImdbId(string imdbId, int? limit = null);
    Task<MediaItem> SaveAsync(MediaItem item);
    Task<IEnumerable<MediaItem>> SaveRangeAsync(IEnumerable<MediaItem> items);
    Task<int> CountAsync();
    Task<bool> ExistsByGuidAsync(string guid);
}

