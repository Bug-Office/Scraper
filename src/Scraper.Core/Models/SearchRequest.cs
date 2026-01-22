namespace Scraper.Core.Models;

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? ImdbId { get; set; }
    public MediaType? Type { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
}

