namespace Scraper.Core.Interfaces;

public interface ITmdbService
{
    Task<string?> GetImdbIdByTitleAsync(string title, int? year = null, CancellationToken cancellationToken = default);
}

