using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface ITmdbService
{
    Task<TmdbMovieDetails?> GetTmdbMovieDetailsByTitleAsync(string title, int? year = null, CancellationToken cancellationToken = default);
    Task<TmdbMovieDetails?> GetTmdbMovieDetailsByExternalSource(string externalId, string externalSource, CancellationToken cancellationToken = default);
}

