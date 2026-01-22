namespace Scraper.Core.Interfaces;

public interface IScraperService
{
    Task<IEnumerable<Models.MediaItem>> SearchAsync(Models.SearchRequest request, CancellationToken cancellationToken = default);
}

