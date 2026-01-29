using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scraper.Core.Normalizers;

public class TitleNormalizer : ITitleNormalizer
{
    private readonly Regex ResolutionRegex = new(@"(?i)(\d{3,4}p|2160p|1080p|720p|480p|360p)", RegexOptions.Compiled);

    public string NormalizeTitle(string? title = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var normalized = title.Trim();

        // Remove common prefixes/suffixes
        normalized = Regex.Replace(normalized, @"(?i)^\[.*?\]\s*", "");
        normalized = Regex.Replace(normalized, @"(?i)\s*\[.*?\]$", "");

        // Remove file extensions
        normalized = Regex.Replace(normalized, @"\.(mkv|mp4|avi|torrent)$", "", RegexOptions.IgnoreCase);

        // Clean up multiple spaces
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return CleanTitleForTmdb(normalized).Trim();
    }

    public string NormalizeTitle(MediaItem item)
    {
        if (string.IsNullOrWhiteSpace(item.NormalizedTitle))
            return string.Empty;

        var title = item.NormalizedTitle;

        // Remove year
        if (item.ReleaseDate != default)
        {
            title = Regex.Replace(title, $@"\(?{item.ReleaseDate.Year}\)?", "", RegexOptions.IgnoreCase);
        }

        // Tokens to remove (format + resolution)
        var tokensToRemove = new HashSet<string>(
            (
                (item.Format ?? "")
                    .Split(" / ", StringSplitOptions.RemoveEmptyEntries)
                    .Concat(
                        (item.Resolution ?? "")
                            .Split(" / ", StringSplitOptions.RemoveEmptyEntries)
                    )
            )
            .Select(t => t.ToLowerInvariant().Trim())
        );

        // Split title into words
        var words = title
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !tokensToRemove.Contains(word.ToLowerInvariant()))
            .ToList();

        // Rebuild title
        var normalizedTitle = string.Join(" ", words);

        // Normalize spaces
        normalizedTitle = Regex.Replace(normalizedTitle, @"\s{2,}", " ").Trim();

        // Normalize
        normalizedTitle = Regex.Replace(normalizedTitle, @" - ", ": ").Trim();
        normalizedTitle = Regex.Replace(normalizedTitle, @"- ", ": ").Trim();

        // Capitalize (pt-BR)
        normalizedTitle = CultureInfo
            .GetCultureInfo("pt-BR")
            .TextInfo
            .ToTitleCase(normalizedTitle.ToLowerInvariant());

        return normalizedTitle;
    }

    public List<MediaLanguage> DetectLanguages(string title)
    {
        var languages = new List<MediaLanguage>();
        
        if (string.IsNullOrWhiteSpace(title))
            return languages;

        var lowerTitle = title.ToLowerInvariant();

        // Check for DUAL (both PT-BR and original audio)
        if (lowerTitle.Contains("dual") || lowerTitle.Contains("duplo"))
        {
            languages.Add(MediaLanguage.Portuguese);
            languages.Add(MediaLanguage.English);
            return languages;
        }

        // Check for LEGENDADO (subtitled)
        if (lowerTitle.Contains("legendado") || lowerTitle.Contains("leg") || lowerTitle.Contains("sub"))
        {
            languages.Add(MediaLanguage.English);
        }

        // Check for PT-BR (dubbed)
        if (lowerTitle.Contains("pt-br") || lowerTitle.Contains("ptbr") || 
            lowerTitle.Contains("dublado") || lowerTitle.Contains("dub"))
        {
            if (!languages.Contains(MediaLanguage.Portuguese))
                languages.Add(MediaLanguage.Portuguese);
        }

        return languages;
    }

    public string DetectResolution(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var match = ResolutionRegex.Match(title);
        if (match.Success)
        {
            var resolution = match.Groups[1].Value.ToLowerInvariant();
            return resolution switch
            {
                "2160p" or "4k" => "2160p",
                "1080p" or "full hd" => "1080p",
                "720p" or "hd" => "720p",
                "480p" => "480p",
                _ => resolution
            };
        }

        return string.Empty;
    }

    public string GenerateSafeReleaseName(MediaItem item)
    {
        var parts = new List<string>();

        // Add normalized title
        if (!string.IsNullOrWhiteSpace(item.NormalizedTitle))
            parts.Add(item.NormalizedTitle);

        // Add year if available (extract from title or use current year as fallback)
        parts.Add($"({item.ReleaseDate.Year})");

        // Add resolution
        if (!string.IsNullOrWhiteSpace(item.Resolution))
            parts.Add($"{item.Resolution}");

        // Add language tag
        var languageTag = GenerateLanguageTag(item.Languages);
        if (!string.IsNullOrWhiteSpace(languageTag))
            parts.Add(languageTag);

        //// Add format (BluRay, WEB-DL, etc.) if detected
        //var format = DetectFormat(item.Title);
        //if (!string.IsNullOrWhiteSpace(format))
        //    parts.Add($"{format}");

        return string.Join(" ", parts).Trim().Replace("/", "");
    }

    private readonly string[] GarbageTerms =
    {
        "dual áudio", "torrent", "dublado", "dual", "legendado",
        "download", "web-dl", "bluray", "brrip", "hdrip",
        "x264", "x265", "h264", "h265"
    };

    public string CleanTitleForTmdb(string rawTitle)
    {
        var title = rawTitle.ToLowerInvariant();

        foreach (var term in GarbageTerms)
            title = Regex.Replace(title, $@"\b{Regex.Escape(term)}\b", "", RegexOptions.IgnoreCase);

        // remove [tags]
        title = Regex.Replace(title, @"\[[^\]]*\]", "");

        // remove (tags)
        title = Regex.Replace(title, @"\([^\)]*\)", "");

        // remove separadores ruins
        title = title.Replace("/", " ").Replace("|", " ");

        // normaliza espaços
        title = Regex.Replace(title, @"\s{2,}", " ").Trim();

        // capitaliza
        return CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(title);
    }

    //public string GenerateSceneReleaseName(MediaItem item)
    //{
    //    // 1 base: título original (EN) se existir
    //    var baseTitle = !string.IsNullOrWhiteSpace(item.Title)
    //        ? item.Title
    //        : item.NormalizedTitle;

    //    baseTitle = baseTitle
    //        .Replace(":", "")
    //        .Replace("-", "")
    //        .Replace("'", "")
    //        .Trim();

    //    baseTitle = Regex.Replace(baseTitle, @"\s+", ".");

    //    var parts = new List<string>
    //{
    //    baseTitle
    //};

    //    // 2 ano
    //    parts.Add(item.ReleaseDate.Year.ToString());

    //    // 3 resolu��o
    //    if (!string.IsNullOrWhiteSpace(item.Resolution))
    //        parts.Add(item.Resolution);

    //    // 4 source
    //    parts.Add("WEB-DL");

    //    // 5 idioma
    //    if (item.Languages.Contains(MediaLanguage.Portuguese) && item.Languages.Contains(MediaLanguage.Legendado))
    //        parts.Add("DUAL");
    //    else if (item.Languages.Contains(MediaLanguage.PtBr))
    //        parts.Add("PTBR");
    //    else if (item.Languages.Contains(MediaLanguage.Legendado))
    //        parts.Add("LEG");

    //    return string.Join(".", parts);
    //}

    private string GenerateLanguageTag(List<MediaLanguage> languages)
    {
        var language = "";
        if (languages == null || languages.Count == 0)
            language += "port eng"; // Default

        var hasPortuguese = languages?.Contains(MediaLanguage.Portuguese) ?? false;
        var hasEnglish = languages?.Contains(MediaLanguage.English) ?? false;
        var hasJapanese = languages?.Contains(MediaLanguage.Japanese) ?? false;
        var hasUnknown = languages?.Contains(MediaLanguage.Unknown) ?? false;

        if (hasPortuguese)
            language += " por";
        if (hasEnglish)
            language += " eng";
        if (hasJapanese)
            language += " jap";
        if (hasUnknown)
            language += " eng";

        return language;
    }
}

