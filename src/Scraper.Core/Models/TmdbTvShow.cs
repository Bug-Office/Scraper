using System.Text.Json.Serialization;

namespace Scraper.Core.Models
{
    public class TmdbSearchTvShowResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbTvShowResult> Results { get; set; } = new();
    }

    public class TmdbFindTvShowResponse
    {
        [JsonPropertyName("tv_results")]
        public List<TmdbTvShowResult> Results { get; set; } = new();
    }

    public class TmdbTvShowResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("imdb_id")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("first_air_date")]
        public string? ReleaseDate { get; set; }
    }
}
