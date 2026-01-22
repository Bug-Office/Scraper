using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Scrapers;

public abstract class BaseScraper : IScraper
{
    protected readonly ILogger Logger;
    protected readonly ITitleNormalizer TitleNormalizer;
    protected readonly HttpClient HttpClient;
    protected readonly IFlareSolverrService? FlareSolverrService;

    protected BaseScraper(
        HttpClient httpClient,
        ITitleNormalizer titleNormalizer,
        ILogger logger,
        IFlareSolverrService? flareSolverrService = null)
    {
        HttpClient = httpClient;
        TitleNormalizer = titleNormalizer;
        Logger = logger;
        FlareSolverrService = flareSolverrService;
    }

    public abstract string Name { get; }
    public abstract bool IsEnabled { get; }

    public abstract Task<IEnumerable<MediaItem>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    protected virtual async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try FlareSolverr first if configured
            if (FlareSolverrService != null && FlareSolverrService.IsConfigured)
            {
                try
                {
                    var html = await FlareSolverrService.FetchHtmlAsync(url, cancellationToken);
                    if (!string.IsNullOrEmpty(html))
                    {
                        return html;
                    }
                }
                catch (TaskCanceledException)
                {
                    Logger.LogWarning("FlareSolverr request was canceled/timed out for {Url}, falling back to direct HTTP", url);
                }
                catch (TimeoutException)
                {
                    Logger.LogWarning("FlareSolverr timeout for {Url}, falling back to direct HTTP", url);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "FlareSolverr failed for {Url}, falling back to direct HTTP", url);
                }
            }

            try
            {
                var response = await HttpClient.GetStringAsync(url);
                return response;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning("Request was canceled for {Url}", url);
                throw;
            }
            catch (TaskCanceledException)
            {
                Logger.LogWarning("Request timed out for {Url}. The website may be slow or unresponsive.", url);
                throw new TimeoutException($"Request to {url} timed out after {HttpClient.Timeout.TotalSeconds} seconds");
            }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning(ex, "HTTP error fetching {Url}. The website may be down or blocking requests.", url);
                throw;
            }
        }
        catch (TimeoutException)
        {
            // Re-throw timeout exceptions
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching HTML from {Url}", url);
            throw;
        }
    }

    protected virtual HtmlDocument ParseHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    protected virtual MediaItem CreateMediaItem(
        string title,
        string link,
        long? size = null,
        DateTime? publishDate = null,
        MediaType type = MediaType.Unknown)
    {
        var normalizedTitle = TitleNormalizer.NormalizeTitle(title, type);
        var language = TitleNormalizer.DetectLanguage(title);
        var resolution = TitleNormalizer.DetectResolution(title);

        var item = new MediaItem
        {
            Title = title,
            NormalizedTitle = normalizedTitle,
            Language = language,
            Resolution = resolution,
            Type = type,
            PublishDate = publishDate ?? DateTime.UtcNow,
            Guid = Guid.NewGuid().ToString()
        };

        // Determine if it's a magnet or torrent link
        if (link.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            item.MagnetLink = link;
        }
        else
        {
            item.TorrentLink = link;
        }

        if (size.HasValue)
        {
            item.FileSize = size.Value;
        }

        return item;
    }

    protected virtual long ParseFileSize(string? sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
            return 0;

        var normalized = sizeText.Trim().ToUpperInvariant();
        var multiplier = 1L;

        if (normalized.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L * 1024L;
            normalized = normalized.Replace("GB", "").Trim();
        }
        else if (normalized.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L;
            normalized = normalized.Replace("MB", "").Trim();
        }
        else if (normalized.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L;
            normalized = normalized.Replace("KB", "").Trim();
        }

        if (double.TryParse(normalized, out var value))
        {
            return (long)(value * multiplier);
        }

        return 0;
    }

    protected virtual DateTime ParseDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return DateTime.UtcNow;

        // Try common date formats
        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy",
            "dd.MM.yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateText, format, null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        // Fallback to TryParse
        if (DateTime.TryParse(dateText, out var parsedDate))
        {
            return parsedDate;
        }

        return DateTime.UtcNow;
    }
}

