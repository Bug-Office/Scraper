using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Scraper.Core.Models;
using Scraper.Infrastructure.Data;
using Scraper.Infrastructure.Data.Entities;
using Scraper.Infrastructure.Interfaces;

namespace Scraper.Infrastructure.Repositories;

public class MediaItemRepository : IMediaItemRepository
{
    private readonly ScraperDbContext _context;
    private readonly ILogger<MediaItemRepository> _logger;
    /// <summary>Serializa acesso ao DbContext para evitar uso concorrente (não é thread-safe).</summary>
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public MediaItemRepository(ScraperDbContext context, ILogger<MediaItemRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MediaItem?> GetByGuidAsync(string guid)
    {
        await _dbLock.WaitAsync();
        try
        {
            var entity = await _context.MediaItems
            .FirstOrDefaultAsync(e => e.Guid == guid);

            return entity?.ToMediaItem();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IEnumerable<MediaItem>> GetByQueryAsync(string query, int? limit = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            var queryLower = query.ToLowerInvariant();

                IQueryable<MediaItemEntity> queryable = _context.MediaItems
                .Where(e => e.Title.ToLower().Contains(queryLower) ||
                       e.NormalizedTitle.ToLower().Contains(queryLower))
                .OrderByDescending(e => e.ReleaseDate);

            if (limit.HasValue)
            {
                queryable = queryable.Take(limit.Value);
            }

            var entities = await queryable.ToListAsync();
            return entities.Select(e => e.ToMediaItem());
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IEnumerable<MediaItem>> GetByPageUrlAsync(string pageUrl, int? limit = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            var pageUrlLower = pageUrl.ToLowerInvariant();

            IQueryable<MediaItemEntity> queryable = _context.MediaItems
                .Where(e => e.PageUrl.ToLower().Contains(pageUrlLower))
                .OrderByDescending(e => e.ReleaseDate);

            if (limit.HasValue)
            {
                queryable = queryable.Take(limit.Value);
            }

            var entities = await queryable.ToListAsync();
            return entities.Select(e => e.ToMediaItem());
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IEnumerable<MediaItem>> GetByImdbId(string imdbid, int? limit = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            IQueryable<MediaItemEntity> queryable = _context.MediaItems
                .Where(e => e.ImdbId.Contains(imdbid))
                .OrderByDescending(e => e.ReleaseDate);

            if (limit.HasValue)
            {
                queryable = queryable.Take(limit.Value);
            }

            var entities = await queryable.ToListAsync();
            return entities.Select(e => e.ToMediaItem());
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<MediaItem> SaveAsync(MediaItem item)
    {
        await _dbLock.WaitAsync();
        try
        {
            var existing = await _context.MediaItems
                .FirstOrDefaultAsync(e => e.Guid == item.Guid);

            if (existing != null)
            {
                // Atualizar item existente
                existing.Title = item.Title;
                existing.NormalizedTitle = item.NormalizedTitle;
                existing.PageUrl = item.PageUrl;
                existing.MagnetLink = item.MagnetLink;
                existing.TorrentLink = item.TorrentLink;
                existing.FileSize = item.FileSize;
                existing.ReleaseDate = item.ReleaseDate;
                existing.Resolution = item.Resolution;
                existing.Format = item.Format;
                existing.LanguagesJson = JsonSerializer.Serialize(item.Languages ?? new List<MediaLanguage>());
                existing.Type = (int)item.Type;
                existing.ImdbId = item.ImdbId;
                existing.TmdbId = item.TmdbId;
                existing.Description = item.Description;
                existing.Seeders = item.Seeders;
                existing.Leechers = item.Leechers;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated MediaItem with Guid: {Guid}", item.Guid);
                return existing.ToMediaItem();
            }
            else
            {
                // Criar novo item
                var entity = MediaItemEntity.FromMediaItem(item);
                _context.MediaItems.Add(entity);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Saved new MediaItem with Guid: {Guid}", item.Guid);
                return entity.ToMediaItem();
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IEnumerable<MediaItem>> SaveRangeAsync(IEnumerable<MediaItem> items)
    {
        await _dbLock.WaitAsync();
        try
        {
            var itemsList = items.ToList();
            var guids = itemsList.Select(i => i.Guid).ToList();

            // Buscar itens existentes
            var existingEntities = await _context.MediaItems
                .Where(e => guids.Contains(e.Guid))
                .ToListAsync();

            var existingGuids = existingEntities.Select(e => e.Guid).ToHashSet();
            var newItems = itemsList.Where(i => !existingGuids.Contains(i.Guid)).ToList();
            var updateItems = itemsList.Where(i => existingGuids.Contains(i.Guid)).ToList();

            // Atualizar existentes
            foreach (var item in updateItems)
            {
                var entity = existingEntities.First(e => e.Guid == item.Guid);
                entity.Title = item.Title;
                entity.NormalizedTitle = item.NormalizedTitle;
                entity.PageUrl = item.PageUrl;
                entity.MagnetLink = item.MagnetLink;
                entity.TorrentLink = item.TorrentLink;
                entity.FileSize = item.FileSize;
                entity.ReleaseDate = item.ReleaseDate;
                entity.Resolution = item.Resolution;
                entity.Format = item.Format;
                entity.LanguagesJson = JsonSerializer.Serialize(item.Languages ?? new List<MediaLanguage>());
                entity.Type = (int)item.Type;
                entity.ImdbId = item.ImdbId;
                entity.Description = item.Description;
                entity.Seeders = item.Seeders;
                entity.Leechers = item.Leechers;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            // Adicionar novos
            var newEntities = newItems.Select(MediaItemEntity.FromMediaItem).ToList();
            _context.MediaItems.AddRange(newEntities);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved {NewCount} new items and updated {UpdateCount} existing items",
                newItems.Count, updateItems.Count);

            return itemsList;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> CountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            return await _context.MediaItems.CountAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> ExistsByGuidAsync(string guid)
    {
        await _dbLock.WaitAsync();
        try
        {
            return await _context.MediaItems.AnyAsync(e => e.Guid == guid);
        }
        finally
        {
            _dbLock.Release();
        }
    }
}

