# FlareSolverr Integration Guide

This document explains how to integrate FlareSolverr for Cloudflare-protected websites.

## What is FlareSolverr?

FlareSolverr is a proxy server that bypasses Cloudflare protection by solving JavaScript challenges. It's useful when scraping websites protected by Cloudflare.

## Setup

1. **Run FlareSolverr** (using Docker):

```bash
docker run -d \
  --name=flaresolverr \
  -p 8191:8191 \
  -e LOG_LEVEL=info \
  --restart unless-stopped \
  ghcr.io/flaresolverr/flaresolverr:latest
```

2. **Modify your scraper** to use FlareSolverr:

```csharp
public class CloudflareProtectedScraper : BaseScraper
{
    private readonly string _flareSolverrUrl;

    public CloudflareProtectedScraper(
        ITitleNormalizer titleNormalizer,
        ILogger<CloudflareProtectedScraper> logger,
        IConfiguration configuration)
        : base(
            CreateFlareSolverrClient(),
            titleNormalizer,
            logger)
    {
        _flareSolverrUrl = configuration["FlareSolverr:Url"] ?? "http://localhost:8191";
    }

    private static HttpClient CreateFlareSolverrClient()
    {
        // Create a client that proxies through FlareSolverr
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy("http://localhost:8191")
        };
        return new HttpClient(handler);
    }

    protected override async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        // Use FlareSolverr API to solve Cloudflare challenge
        var flareRequest = new
        {
            cmd = "request.get",
            url = url,
            maxTimeout = 60000
        };

        var json = System.Text.Json.JsonSerializer.Serialize(flareRequest);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await HttpClient.PostAsync(_flareSolverrUrl + "/v1", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var flareResponse = System.Text.Json.JsonSerializer.Deserialize<FlareSolverrResponse>(responseJson);

        if (flareResponse?.Status == "ok" && flareResponse.Solution?.Response != null)
        {
            return flareResponse.Solution.Response;
        }

        throw new Exception("FlareSolverr failed to solve challenge");
    }

    private class FlareSolverrResponse
    {
        public string Status { get; set; } = string.Empty;
        public FlareSolverrSolution? Solution { get; set; }
    }

    private class FlareSolverrSolution
    {
        public string? Response { get; set; }
    }
}
```

## Configuration

Add FlareSolverr URL to `appsettings.json`:

```json
{
  "FlareSolverr": {
    "Url": "http://localhost:8191"
  }
}
```

## Notes

- FlareSolverr adds latency to requests (typically 5-30 seconds)
- Use caching to minimize FlareSolverr calls
- Consider rate limiting to avoid overwhelming FlareSolverr
- FlareSolverr may require periodic restarts if it gets blocked

## Alternative: Direct HTTP with Headers

Some Cloudflare-protected sites can be accessed with proper headers and cookies without FlareSolverr. Try this first:

```csharp
var client = HttpClientFactory.CreateClient(
    baseUrl: "https://example.com",
    headers: new Dictionary<string, string>
    {
        { "User-Agent", "Mozilla/5.0..." },
        { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
        { "Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7" },
        { "Accept-Encoding", "gzip, deflate, br" },
        { "Connection", "keep-alive" },
        { "Upgrade-Insecure-Requests", "1" }
    },
    cookies: new Dictionary<string, string>
    {
        { "cf_clearance", "your-clearance-cookie" }
    }
);
```

