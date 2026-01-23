using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scraper.Core.Models;
using Scraper.Infrastructure.Data;
using Scraper.Infrastructure.Data.Entities;
using Scraper.Infrastructure.Interfaces;

namespace Scraper.Infrastructure.Repositories;

public class MediaItemRepository : IMediaItemRepository
{
    private readonly ScraperDbContext _context;
    private readonly ILogger<MediaItemRepository> _logger;

    public MediaItemRepository(ScraperDbContext context, ILogger<MediaItemRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MediaItem?> GetByGuidAsync(string guid)
    {
        var entity = await _context.MediaItems
            .FirstOrDefaultAsync(e => e.Guid == guid);

        return entity?.ToMediaItem();
    }

    public async Task<IEnumerable<MediaItem>> GetByQueryAsync(string query, int? limit = null)
    {
        var queryLower = query.ToLowerInvariant();
        
        IQueryable<MediaItemEntity> queryable = _context.MediaItems
            .Where(e => e.Title.ToLower().Contains(queryLower) || 
                       e.NormalizedTitle.ToLower().Contains(queryLower))
            .OrderByDescending(e => e.PublishDate);

        if (limit.HasValue)
        {
            queryable = queryable.Take(limit.Value);
        }

        var entities = await queryable.ToListAsync();
        return entities.Select(e => e.ToMediaItem());
    }

    public async Task<IEnumerable<MediaItem>> GetByPageUrlAsync(string pageUrl, int? limit = null)
    {
        var pageUrlLower = pageUrl.ToLowerInvariant();

        IQueryable<MediaItemEntity> queryable = _context.MediaItems
            .Where(e => e.PageUrl.ToLower().Contains(pageUrlLower))
            .OrderByDescending(e => e.PublishDate);

        if (limit.HasValue)
        {
            queryable = queryable.Take(limit.Value);
        }

        var entities = await queryable.ToListAsync();
        return entities.Select(e => e.ToMediaItem());
    }

    public async Task<IEnumerable<MediaItem>> GetByImdbid(string imdbid, int? limit = null)
    {
        IQueryable<MediaItemEntity> queryable = _context.MediaItems
            .Where(e => e.ImdbId.Contains(imdbid))
            .OrderByDescending(e => e.PublishDate);

        if (limit.HasValue)
        {
            queryable = queryable.Take(limit.Value);
        }

        var entities = await queryable.ToListAsync();
        return entities.Select(e => e.ToMediaItem());
    }

    public async Task<MediaItem> SaveAsync(MediaItem item)
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
            existing.PublishDate = item.PublishDate;
            existing.Resolution = item.Resolution;
            existing.Language = (int)item.Language;
            existing.Type = (int)item.Type;
            existing.ImdbId = item.ImdbId;
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

    public async Task<IEnumerable<MediaItem>> SaveRangeAsync(IEnumerable<MediaItem> items)
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
            entity.PublishDate = item.PublishDate;
            entity.Resolution = item.Resolution;
            entity.Language = (int)item.Language;
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

    public async Task<int> CountAsync()
    {
        return await _context.MediaItems.CountAsync();
    }

    public async Task<bool> ExistsByGuidAsync(string guid)
    {
        return await _context.MediaItems.AnyAsync(e => e.Guid == guid);
    }
}

