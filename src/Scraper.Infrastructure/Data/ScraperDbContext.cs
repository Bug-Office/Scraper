using Microsoft.EntityFrameworkCore;
using Scraper.Infrastructure.Data.Entities;

namespace Scraper.Infrastructure.Data;

public class ScraperDbContext : DbContext
{
    public ScraperDbContext(DbContextOptions<ScraperDbContext> options) : base(options)
    {
    }

    public DbSet<MediaItemEntity> MediaItems { get; set; }
    public DbSet<ConfigurationEntity> Configurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração para MediaItemEntity
        modelBuilder.Entity<MediaItemEntity>(entity =>
        {
            entity.HasIndex(e => e.Guid).IsUnique();
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.NormalizedTitle);
            entity.HasIndex(e => e.ImdbId);
            entity.HasIndex(e => e.PublishDate);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configuração para ConfigurationEntity
        modelBuilder.Entity<ConfigurationEntity>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }
}

