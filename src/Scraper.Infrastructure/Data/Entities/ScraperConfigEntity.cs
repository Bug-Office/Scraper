using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Scraper.Infrastructure.Data.Entities;

[Table("ScraperConfigs")]
public class ScraperConfigEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    [Required]
    [Column(TypeName = "TEXT")]
    public string SettingsJson { get; set; } = "{}"; // Dictionary<string, string> serializado como JSON

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    // Método para converter de ScraperConfig para Entity
    public static ScraperConfigEntity FromScraperConfig(Scraper.Core.Models.ScraperConfig config)
    {
        return new ScraperConfigEntity
        {
            Name = config.Name,
            IsEnabled = config.IsEnabled,
            SettingsJson = JsonSerializer.Serialize(config.Settings ?? new Dictionary<string, string>(), JsonOptions),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // Método para converter de Entity para ScraperConfig
    public Scraper.Core.Models.ScraperConfig ToScraperConfig()
    {
        var settings = DeserializeSettings(SettingsJson);
        return new Scraper.Core.Models.ScraperConfig
        {
            Name = Name,
            IsEnabled = IsEnabled,
            Settings = settings
        };
    }

    private static Dictionary<string, string> DeserializeSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new Dictionary<string, string>();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
            return settings ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
