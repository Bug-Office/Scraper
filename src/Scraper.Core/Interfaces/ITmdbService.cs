using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface ITmdbService
{
    Task<dynamic?> GetTmdbDetailsByTitleAsync(string title, int? year = null, MediaType? type = MediaType.Unknown, CancellationToken cancellationToken = default);
    Task<dynamic?> GetTmdbDetailsByExternalSourceAsync(string title, MediaType? type = MediaType.Unknown, CancellationToken cancellationToken = default);
    Task<TmdbMovieResult?> GetTmdbMovieDetailsByTitleAsync(string title, int? year = null, CancellationToken cancellationToken = default);
    Task<TmdbMovieResult?> GetTmdbMovieDetailsByExternalSourceAsync(string externalId, string externalSource, CancellationToken cancellationToken = default);
    Task<TmdbTvShowResult?> GetTmdbTvShowDetailsByTitleAsync(string title, int? year = null, CancellationToken cancellationToken = default);
    Task<TmdbTvShowResult?> GetTmdbTvShowDetailsByExternalSourceAsync(string externalId, string externalSource, CancellationToken cancellationToken = default);
}

