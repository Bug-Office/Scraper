using System.Text.Json.Serialization;

namespace Scraper.Core.Models
{
    public class TmdbSearchMovieResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbMovieResult> Results { get; set; } = new();
    }

    public class TmdbFindMovieResponse
    {
        [JsonPropertyName("movie_results")]
        public List<TmdbMovieResult> Results { get; set; } = new();
    }

    public class TmdbMovieResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("imdb_id")]
        public string? ImdbId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }
    }
}
