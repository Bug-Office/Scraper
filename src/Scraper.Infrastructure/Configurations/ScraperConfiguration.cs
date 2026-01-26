namespace Scraper.Infrastructure.Configurations;

/// <summary>
/// Configuration class for scraper selectors and behavior
/// </summary>
public class ScraperConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string SearchUrlTemplate { get; set; } = string.Empty;
    
    /// <summary>
    /// XPath selectors for finding result items on search page
    /// </summary>
    public List<string> ResultItemSelectors { get; set; } = new();

    /// <summary>
    /// XPath selector for finding title in result item
    /// </summary>
    public List<string> TitleSelectors { get; set; } = new();
    
    /// <summary>
    /// XPath selector for finding title link in result item
    /// </summary>
    public List<string> TitleLinkSelectors { get; set; } = new();
    
    /// <summary>
    /// XPath selector for finding download section on detail page
    /// </summary>
    public List<string> DownloadSectionSelectors { get; set; } = new();
    
    /// <summary>
    /// XPath selector for finding episode paragraphs on detail page
    /// </summary>
    public List<string> EpisodeParagraphSelectors { get; set; } = new();
    
    /// <summary>
    /// XPath selector for finding info section on detail page
    /// </summary>
    public List<string> InfoSectionSelectors { get; set; } = new();
    
    /// <summary>
    /// Patterns for cleaning titles
    /// </summary>
    public List<string> TitleCleanupPatterns { get; set; } = new();
}
