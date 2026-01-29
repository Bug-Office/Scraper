using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Services;

public class TmdbService : ITmdbService
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbService> _logger;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private string? _apiKey;

    public TmdbService(
        HttpClient httpClient,
        ILogger<TmdbService> logger,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        
        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");

        _apiKey = GetApiKeyAsync().GetAwaiter().GetResult();

        // Set authorization header (remove old one if exists, then add new)
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    private async Task<string?> GetApiKeyAsync()
    {

        if (_serviceScopeFactory == null)
            return null;

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = await configService.GetConfigurationAsync();
            return config.TmdbApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get TMDB API key from configuration");
            return null;
        }
    }

    public async Task<dynamic?> GetTmdbDetailsByTitleAsync(string title, int? year = null, MediaType? type = MediaType.Unknown, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogDebug("TMDB API key not configured, skipping IMDB ID lookup");
            return null;
        }

        if (type == MediaType.Movie)
            return await GetTmdbMovieDetailsByTitleAsync(title, year, cancellationToken);

        if (type == MediaType.TvShow)
            return await GetTmdbTvShowDetailsByTitleAsync(title, year, cancellationToken);

        return null;
    }

    public async Task<dynamic?> GetTmdbDetailsByExternalSourceAsync(string title, MediaType? type = MediaType.Unknown, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogDebug("TMDB API key not configured, skipping IMDB ID lookup");
            return null;
        }

        if(type == MediaType.Movie)
            return await GetTmdbMovieDetailsByExternalSourceAsync(title, "imdb_id", cancellationToken);

        if (type == MediaType.TvShow)
            return await GetTmdbTvShowDetailsByExternalSourceAsync(title, "imdb_id", cancellationToken);

        return null;
    }

    public async Task<TmdbMovieResult?> GetTmdbMovieDetailsByTitleAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {        

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        try
        {
            // Step 1: Search for movie by title
            var searchUrl = $"{BaseUrl}/search/movie?query={Uri.EscapeDataString(title)}";
            if (year.HasValue)
            {
                searchUrl += $"&year={year.Value}";
            }
            searchUrl += "&page=1&language=pt-br";

            _logger.LogDebug("Searching TMDB for movie: {Title} (Year: {Year})", title, year);

            var searchResponse = await _httpClient.GetFromJsonAsync<TmdbSearchMovieResponse>(searchUrl, cancellationToken);

            if (searchResponse?.Results == null || !searchResponse.Results.Any())
            {
                _logger.LogDebug("No results found in TMDB for: {Title}", title);
                return null;
            }

            // Get the first result (most relevant)
            var movie = searchResponse.Results.First();
            var movieId = movie.Id;

            _logger.LogDebug("Found TMDB movie ID {MovieId} for: {Title}", movieId, title);

            // Step 2: Get movie details to retrieve IMDB ID
            var detailsUrl = $"{BaseUrl}/movie/{movieId}?language=pt-br";
            var detailsResponse = await _httpClient.GetFromJsonAsync<TmdbMovieResult>(detailsUrl, cancellationToken);

            if (detailsResponse?.ImdbId == null)
            {
                _logger.LogDebug("No IMDB ID found for TMDB movie ID {MovieId}", movieId);
            }
            else
            {
                _logger.LogDebug("Found IMDB ID {ImdbId} for movie: {Title}", detailsResponse.ImdbId, title);
            }

            return detailsResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while searching TMDB for: {Title}", title);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timeout while searching TMDB for: {Title}", title);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching TMDB for: {Title}", title);
            return null;
        }
    }

    public async Task<TmdbMovieResult?> GetTmdbMovieDetailsByExternalSourceAsync(string externalId, string externalSource = "imdb_id", CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        try
        {
            if(externalSource == "imdb_id" && !externalId.StartsWith("tt"))
            {
                externalId = "tt" + externalId;
            }

            // Step 1: Search for movie by ImdbId
            var searchUrl = $"{BaseUrl}/find/{externalId}?external_source={externalSource}";
            searchUrl += "&page=1&language=pt-br";

            _logger.LogDebug("Searching TMDB movie for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);

            var searchResponse = await _httpClient.GetFromJsonAsync<TmdbFindMovieResponse>(searchUrl, cancellationToken);

            if (searchResponse?.Results == null || !searchResponse.Results.Any())
            {
                _logger.LogDebug("No results found in TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
                return null;
            }

            // Get the first result (most relevant)
            var movie = searchResponse.Results.First();
            var movieId = movie.Id;
            var movieTitle = movie.Title;

            _logger.LogDebug("Found TMDB movie ID {MovieId} for: {Title}", movieId, movieTitle);

            // Step 2: Get movie details to retrieve IMDB ID
            var detailsUrl = $"{BaseUrl}/movie/{movieId}?language=pt-br";
            var detailsResponse = await _httpClient.GetFromJsonAsync<TmdbMovieResult>(detailsUrl, cancellationToken);

            _logger.LogDebug("Found IMDB ID {ImdbId} for movie: {Title}", detailsResponse.ImdbId, movieTitle);
            return detailsResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while searching TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timeout while searching TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
            return null;
        }
    }

    public async Task<TmdbTvShowResult?> GetTmdbTvShowDetailsByTitleAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        try
        {
            // Step 1: Search for movie by title
            var searchUrl = $"{BaseUrl}/search/tv?query={Uri.EscapeDataString(title)}";
            if (year.HasValue)
            {
                searchUrl += $"&year={year.Value}";
            }
            searchUrl += "&page=1&language=pt-br";

            _logger.LogDebug("Searching TMDB for TV: {Title} (Year: {Year})", title, year);

            var searchResponse = await _httpClient.GetFromJsonAsync<TmdbSearchTvShowResponse>(searchUrl, cancellationToken);

            if (searchResponse?.Results == null || !searchResponse.Results.Any())
            {
                _logger.LogDebug("No results found in TMDB for: {Title}", title);
                return null;
            }

            // Get the first result (most relevant)
            var tvShow = searchResponse.Results.First();
            var tvShowId = tvShow.Id;

            _logger.LogDebug("Found TMDB movie ID {TvShowId} for: {Title}", tvShowId, title);

            // Step 2: Get tvshow details to retrieve IMDB ID
            var detailsUrl = $"{BaseUrl}/tv/{tvShowId}?language=pt-br";
            var detailsResponse = await _httpClient.GetFromJsonAsync<TmdbTvShowResult>(detailsUrl, cancellationToken);

            if (detailsResponse?.ImdbId == null)
            {
                _logger.LogDebug("No IMDB ID found for TMDB tvshow ID {TvShowId}", tvShowId);
            }
            else
            {
                _logger.LogDebug("Found IMDB ID {ImdbId} for tvshow: {Title}", detailsResponse.ImdbId, title);
            }

            return detailsResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while searching TMDB for: {Title}", title);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timeout while searching TMDB for: {Title}", title);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching TMDB for: {Title}", title);
            return null;
        }
    }

    public async Task<TmdbTvShowResult?> GetTmdbTvShowDetailsByExternalSourceAsync(string externalId, string externalSource, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        try
        {
            if (externalSource == "imdb_id" && !externalId.StartsWith("tt"))
            {
                externalId = "tt" + externalId;
            }

            // Step 1: Search for movie by ImdbId
            var searchUrl = $"{BaseUrl}/find/{externalId}?external_source={externalSource}";
            searchUrl += "&page=1&language=pt-br";

            _logger.LogDebug("Searching TMDB movie for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);

            var searchResponse = await _httpClient.GetFromJsonAsync<TmdbFindTvShowResponse>(searchUrl, cancellationToken);

            if (searchResponse?.Results == null || !searchResponse.Results.Any())
            {
                _logger.LogDebug("No results found in TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
                return null;
            }

            // Get the first result (most relevant)
            var tvShow = searchResponse.Results.First();
            var tvShowId = tvShow.Id;
            var tvShowTitle = tvShow.Name;

            _logger.LogDebug("Found TMDB movie ID {TvShowId} for: {Title}", tvShowId, tvShowTitle);

            // Step 2: Get movie details to retrieve IMDB ID
            var detailsUrl = $"{BaseUrl}/tv/{tvShowId}?language=pt-br";
            var detailsResponse = await _httpClient.GetFromJsonAsync<TmdbTvShowResult>(detailsUrl, cancellationToken);

            _logger.LogDebug("Found IMDB ID {ImdbId} for movie: {Title}", detailsResponse.ImdbId, tvShowTitle);
            return detailsResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while searching TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request timeout while searching TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching TMDB for: External Id '{ExternalId}' - External Source '{ExternalSource}'", externalId, externalSource);
            return null;
        }
    }
}

