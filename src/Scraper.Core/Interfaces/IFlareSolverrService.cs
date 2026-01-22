namespace Scraper.Core.Interfaces;

public interface IFlareSolverrService
{
    Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}

