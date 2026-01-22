namespace Scraper.Core.Interfaces;

public interface IScraper
{
    string Name { get; }
    Task<IEnumerable<Models.MediaItem>> SearchAsync(Models.SearchRequest request, CancellationToken cancellationToken = default);
    bool IsEnabled { get; }
}

