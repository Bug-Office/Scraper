using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Core.Interfaces;

namespace Scraper.Infrastructure.Services;

public class FlareSolverrService : IFlareSolverrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FlareSolverrService> _logger;
    private readonly IConfigurationService _configurationService;
    private string? _cachedFlareSolverrUrl;
    private DateTime _lastConfigCheck = DateTime.MinValue;
    private readonly TimeSpan _configCacheTime = TimeSpan.FromSeconds(30);

    public FlareSolverrService(
        ILogger<FlareSolverrService> logger,
        IConfigurationService configurationService
    )
    {
        _logger = logger;
        _configurationService = configurationService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(6) // FlareSolverr can take 2-5 minutes, give extra buffer
        };

        // Load initial configuration (defer to avoid issues during service registration)
        // Configuration will be loaded on first use
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _configurationService.GetConfigurationAsync().GetAwaiter().GetResult();
            var newUrl = config.FlareSolverrUrl;

            // Only update if URL changed
            if (_cachedFlareSolverrUrl != newUrl)
            {
                _cachedFlareSolverrUrl = newUrl;

                if (!string.IsNullOrWhiteSpace(_cachedFlareSolverrUrl))
                {
                    _httpClient.BaseAddress = new Uri(_cachedFlareSolverrUrl);
                    _logger.LogInformation("FlareSolverr configured with URL: {Url}", _cachedFlareSolverrUrl);
                }
                else
                {
                    _httpClient.BaseAddress = null;
                    _logger.LogDebug("FlareSolverr not configured");
                }
            }

            _lastConfigCheck = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load FlareSolverr configuration");
        }
    }

    public bool IsConfigured
    {
        get
        {
            // Reload configuration if cache expired
            if (DateTime.UtcNow - _lastConfigCheck > _configCacheTime)
            {
                LoadConfiguration();
            }
            return !string.IsNullOrWhiteSpace(_cachedFlareSolverrUrl);
        }
    }

    public async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        // Reload configuration if cache expired (allows dynamic updates)
        if (DateTime.UtcNow - _lastConfigCheck > _configCacheTime)
        {
            LoadConfiguration();
        }

        if (!IsConfigured)
        {
            _logger.LogDebug("FlareSolverr not configured, cannot fetch {Url}", url);
            return null;
        }

        // Get max timeout from configuration
        int maxTimeout = 240000; // Default 4 minutes
        try
        {
            var config = await _configurationService.GetConfigurationAsync();
            maxTimeout = config.FlareSolverrMaxTimeoutMs > 0 
                ? config.FlareSolverrMaxTimeoutMs 
                : 240000;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get FlareSolverr timeout from configuration, using default");
        }

        try
        {
            _logger.LogDebug("Requesting FlareSolverr to fetch {Url} with maxTimeout: {MaxTimeout}ms", url, maxTimeout);

            var request = new FlareSolverrRequest
            {
                Cmd = "request.get",
                Url = url,
                MaxTimeout = maxTimeout
            };
            
            // Use a timeout based on configuration, but add buffer for HTTP client overhead
            var httpTimeout = TimeSpan.FromMilliseconds(maxTimeout + 10000); // Add 10 seconds buffer
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(httpTimeout);
            
            var response = await _httpClient.PostAsJsonAsync("/v1", request, cts.Token);
            response.EnsureSuccessStatusCode();

            var flareResponse = await response.Content.ReadFromJsonAsync<FlareSolverrResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (flareResponse?.Status == "ok" && flareResponse.Solution != null)
            {
                _logger.LogDebug("FlareSolverr successfully fetched {Url}", url);
                return flareResponse.Solution.Response;
            }

            if (flareResponse?.Status == "error")
            {
                _logger.LogError("FlareSolverr error: {Message}", flareResponse.Message);
                return null;
            }

            _logger.LogWarning("FlareSolverr returned unexpected response for {Url}", url);
            return null;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested || true)
        {
            _logger.LogWarning("FlareSolverr request timed out for {Url} after {Timeout}ms. Falling back to direct HTTP.", url, maxTimeout);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error calling FlareSolverr for {Url}. Is FlareSolverr running? Falling back to direct HTTP.", url);
            return null;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("FlareSolverr timeout for {Url} after {Timeout}ms. Falling back to direct HTTP.", url, maxTimeout);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calling FlareSolverr for {Url}. Falling back to direct HTTP.", url);
            return null;
        }
    }

    private class FlareSolverrRequest
    {
        public string Cmd { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int MaxTimeout { get; set; }
    }

    private class FlareSolverrResponse
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public FlareSolverrSolution? Solution { get; set; }
    }

    private class FlareSolverrSolution
    {
        public string? Response { get; set; }
        public string? Url { get; set; }
        public int StatusCode { get; set; }
    }
}

