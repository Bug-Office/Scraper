using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Data.Entities;

[Table("MediaItems")]
public class MediaItemEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string NormalizedTitle { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string PageUrl { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string MagnetLink { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string TorrentLink { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime ReleaseDate { get; set; }

    [MaxLength(50)]
    public string Resolution { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Format { get; set; } = string.Empty;

    [MaxLength(500)]
    public string LanguagesJson { get; set; } = "[]"; // Lista de enums serializada como JSON

    public int Type { get; set; } // Enum como int

    [MaxLength(50)]
    public string? ImdbId { get; set; }

    [MaxLength(50)]
    public string? TmdbId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? Seeders { get; set; }

    public int? Leechers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Método para converter de MediaItem para Entity
    public static MediaItemEntity FromMediaItem(MediaItem item)
    {
        return new MediaItemEntity
        {
            Title = item.Title,
            NormalizedTitle = item.NormalizedTitle,
            PageUrl = item.PageUrl,
            MagnetLink = item.MagnetLink,
            TorrentLink = item.TorrentLink,
            FileSize = item.FileSize,
            ReleaseDate = item.ReleaseDate,
            Resolution = item.Resolution,
            Format = item.Format,
            LanguagesJson = JsonSerializer.Serialize(item.Languages ?? new List<MediaLanguage>(), JsonOptions),
            Type = (int)item.Type,
            ImdbId = item.ImdbId,
            Guid = item.Guid,
            Description = item.Description,
            Seeders = item.Seeders,
            Leechers = item.Leechers,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Método para converter de Entity para MediaItem
    public MediaItem ToMediaItem()
    {
        return new MediaItem
        {
            Title = Title,
            NormalizedTitle = NormalizedTitle,
            PageUrl = PageUrl,
            MagnetLink = MagnetLink,
            TorrentLink = TorrentLink,
            FileSize = FileSize,
            ReleaseDate = ReleaseDate,
            Resolution = Resolution,
            Format = Format,
            Languages = DeserializeLanguages(LanguagesJson),
            Type = (MediaType)Type,
            ImdbId = ImdbId,
            Guid = Guid,
            Description = Description,
            Seeders = Seeders,
            Leechers = Leechers
        };
    }

    private static List<MediaLanguage> DeserializeLanguages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<MediaLanguage>();

        try
        {
            // Tenta deserializar normalmente (com nomes do enum ou números)
            var languages = JsonSerializer.Deserialize<List<MediaLanguage>>(json, JsonOptions);
            if (languages != null)
                return languages;
        }
        catch (JsonException)
        {
            // Se falhar, tenta outras abordagens
        }

        try
        {
            // Tenta deserializar como números (índices do enum)
            var intList = JsonSerializer.Deserialize<List<int>>(json ?? "[]");
            if (intList != null)
            {
                var languages = new List<MediaLanguage>();
                foreach (var intValue in intList)
                {
                    // Mapear valores antigos do enum para novos
                    var language = intValue switch
                    {
                        1 => MediaLanguage.Portuguese, // PtBr antigo
                        2 => MediaLanguage.Portuguese, // Dual antigo (será adicionado English também)
                        3 => MediaLanguage.English, // Legendado antigo
                        _ => (MediaLanguage)intValue // Tentar usar o valor direto
                    };
                    
                    if (language != MediaLanguage.Unknown && !languages.Contains(language))
                        languages.Add(language);
                    
                    // Se era Dual (2), adicionar English também
                    if (intValue == 2 && !languages.Contains(MediaLanguage.English))
                        languages.Add(MediaLanguage.English);
                }
                return languages;
            }
        }
        catch
        {
            // Continuar para próxima tentativa
        }

        try
        {
            // Tenta deserializar como strings para mapear valores antigos
            var stringList = JsonSerializer.Deserialize<List<string>>(json ?? "[]");
            if (stringList != null)
            {
                var languages = new List<MediaLanguage>();
                foreach (var langStr in stringList)
                {
                    var mappedLanguages = MapOldLanguageToNew(langStr);
                    foreach (var lang in mappedLanguages)
                    {
                        if (lang != MediaLanguage.Unknown && !languages.Contains(lang))
                            languages.Add(lang);
                    }
                }
                return languages;
            }
        }
        catch
        {
            // Se tudo falhar, retorna lista vazia
        }

        return new List<MediaLanguage>();
    }

    private static List<MediaLanguage> MapOldLanguageToNew(string oldValue)
    {
        var result = new List<MediaLanguage>();
        var lowerValue = oldValue?.ToLowerInvariant() ?? "";

        switch (lowerValue)
        {
            case "ptbr":
            case "pt-br":
            case "portuguese":
                result.Add(MediaLanguage.Portuguese);
                break;
            case "dual":
                // Dual significa Portuguese + English
                result.Add(MediaLanguage.Portuguese);
                result.Add(MediaLanguage.English);
                break;
            case "legendado":
            case "leg":
            case "english":
                result.Add(MediaLanguage.English);
                break;
            case "japanese":
            case "jap":
                result.Add(MediaLanguage.Japanese);
                break;
        }

        return result;
    }
}

