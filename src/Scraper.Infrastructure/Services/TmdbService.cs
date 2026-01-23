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
    private string? _cachedApiKey;

    public TmdbService(
        HttpClient httpClient,
        ILogger<TmdbService> logger,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        
        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
    }

    private async Task<string?> GetApiKeyAsync()
    {
        if (_cachedApiKey != null)
            return _cachedApiKey;

        if (_serviceScopeFactory == null)
            return null;

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = await configService.GetConfigurationAsync();
            _cachedApiKey = config.TmdbApiKey;
            return _cachedApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get TMDB API key from configuration");
            return null;
        }
    }

    public async Task<TmdbMovieDetails?> GetTmdbMovieDetailsAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync();
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug("TMDB API key not configured, skipping IMDB ID lookup");
            return null;
        }

        // Set authorization header (remove old one if exists, then add new)
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

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
            searchUrl += "&page=1&language=pt-BR";

            _logger.LogDebug("Searching TMDB for movie: {Title} (Year: {Year})", title, year);

            var searchResponse = await _httpClient.GetFromJsonAsync<TmdbSearchResponse>(searchUrl, cancellationToken);

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
            var detailsUrl = $"{BaseUrl}/movie/{movieId}&language=pt-BR";
            var detailsResponse = await _httpClient.GetFromJsonAsync<TmdbMovieDetails>(detailsUrl, cancellationToken);

            if (detailsResponse?.ImdbId == null)
            {
                _logger.LogDebug("No IMDB ID found for TMDB movie ID {MovieId}", movieId);
                return null;
            }

            _logger.LogDebug("Found IMDB ID {ImdbId} for movie: {Title}", detailsResponse.ImdbId, title);
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
}

