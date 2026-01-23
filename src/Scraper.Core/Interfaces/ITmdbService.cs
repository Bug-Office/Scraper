using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface ITmdbService
{
    Task<TmdbMovieDetails?> GetTmdbMovieDetailsAsync(string title, int? year = null, CancellationToken cancellationToken = default);
}

