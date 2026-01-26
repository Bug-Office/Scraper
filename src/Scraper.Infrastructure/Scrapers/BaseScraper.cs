using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Infrastructure.Interfaces;
using Scraper.Infrastructure.Services;
using System.Globalization;

namespace Scraper.Infrastructure.Scrapers;

public abstract class BaseScraper : IScraper
{
    protected readonly ILogger Logger;
    protected readonly ITitleNormalizer TitleNormalizer;
    protected readonly HttpClient HttpClient;
    protected readonly ITmdbService TmdbService;
    protected readonly IFlareSolverrService? FlareSolverrService;
    protected readonly IMediaItemRepository? MediaItemRepository;

    protected BaseScraper(
        HttpClient httpClient,
        ITitleNormalizer titleNormalizer,
        ILogger logger,
        ITmdbService tmdbService,
        IFlareSolverrService? flareSolverrService = null,
        IMediaItemRepository? mediaItemRepository = null)
    {
        HttpClient = httpClient;
        TitleNormalizer = titleNormalizer;
        Logger = logger;
        TmdbService = tmdbService;
        FlareSolverrService = flareSolverrService;
        MediaItemRepository = mediaItemRepository;
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
        string pageUrl,
        string link,
        long? size = null,
        DateTime? publishDate = null,
        MediaType type = MediaType.Unknown)
    {
        var normalizedTitle = TitleNormalizer.NormalizeTitle(title, type);

        var item = new MediaItem
        {
            Title = title,
            PageUrl = pageUrl,
            NormalizedTitle = normalizedTitle,
            Type = type,
            PublishDate = publishDate ?? DateTime.UtcNow,
            Guid = Guid.NewGuid().ToString(),
            Scraper = Name
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

    protected virtual void NormalizeMediaItem(MediaItem item)
    {
        item.NormalizedTitle = TitleNormalizer.NormalizeTitle(item);

        var tmdbmovieDetails = TmdbService.GetTmdbMovieDetailsByTitleAsync(item.NormalizedTitle).GetAwaiter().GetResult();

        item.Title = tmdbmovieDetails?.Title ?? item.Title;
        item.NormalizedTitle = tmdbmovieDetails?.Title ?? item.NormalizedTitle;
        item.PublishDate = tmdbmovieDetails?.ReleaseDate ?? item.PublishDate;
        item.ImdbId = tmdbmovieDetails?.ImdbId.Split("tt").ElementAt(1);
        item.TmdbId = tmdbmovieDetails?.Id.ToString();
    }
}

