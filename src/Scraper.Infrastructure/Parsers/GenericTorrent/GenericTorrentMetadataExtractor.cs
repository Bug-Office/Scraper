using Scraper.Core.Models;

namespace Scraper.Infrastructure.Parsers.GenericTorrent;

/// <summary>
/// Generic metadata extractor for torrent sites
/// Uses common patterns found in torrent sites
/// </summary>
public class GenericTorrentMetadataExtractor : BaseMetadataExtractor
{
    public override List<MediaLanguage> ExtractLanguages(string? text)
    {
        var languages = new List<MediaLanguage>();
        
        if (string.IsNullOrWhiteSpace(text))
            return languages;

        var lowerText = text.ToLowerInvariant();

        // Check for Dual Audio / Dublado / Dual Áudio
        if (lowerText.Contains("dual") && (lowerText.Contains("áudio") || lowerText.Contains("audio")))
        {
            languages.Add(MediaLanguage.Portuguese);
            languages.Add(MediaLanguage.English);
            return languages;
        }

        // Check for Dublado
        if (lowerText.Contains("dublado"))
        {
            if (!languages.Contains(MediaLanguage.Portuguese))
                languages.Add(MediaLanguage.Portuguese);
        }

        // Check for Japanese
        if (lowerText.Contains("japones"))
        {
            if (!languages.Contains(MediaLanguage.Japanese))
                languages.Add(MediaLanguage.Japanese);
        }

        // Check for English / Legendado
        if (lowerText.Contains("inglês") || lowerText.Contains("legendado"))
        {
            if (!languages.Contains(MediaLanguage.English))
                languages.Add(MediaLanguage.English);
        }

        // Check for PT-BR / Português
        if (lowerText.Contains("português") || lowerText.Contains("pt-br") || lowerText.Contains("ptbr"))
        {
            if (!languages.Contains(MediaLanguage.Portuguese))
                languages.Add(MediaLanguage.Portuguese);
        }

        return languages;
    }
}
