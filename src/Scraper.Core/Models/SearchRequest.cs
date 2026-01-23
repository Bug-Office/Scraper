namespace Scraper.Core.Models;

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 100;
    public string? ImdbId { get; set; }
    public MediaType? Type { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
}

