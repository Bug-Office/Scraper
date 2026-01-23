using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scraper.Core.Normalizers;

public class TitleNormalizer : ITitleNormalizer
{
    private static readonly Regex ResolutionRegex = new(@"(?i)(\d{3,4}p|2160p|1080p|720p|480p|360p)", RegexOptions.Compiled);
    private static readonly Regex YearRegex = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex SeasonEpisodeRegex = new(@"(?i)(s\d{1,2}e\d{1,2}|season\s*\d+|episode\s*\d+)", RegexOptions.Compiled);

    public string NormalizeTitle(string title, MediaType type)
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

    public MediaLanguage DetectLanguage(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return MediaLanguage.Unknown;

        var lowerTitle = title.ToLowerInvariant();

        // Check for DUAL (both PT-BR and original audio)
        if (lowerTitle.Contains("dual") || lowerTitle.Contains("duplo"))
            return MediaLanguage.Dual;

        // Check for LEGENDADO (subtitled)
        if (lowerTitle.Contains("legendado") || lowerTitle.Contains("leg") || lowerTitle.Contains("sub"))
            return MediaLanguage.Legendado;

        // Check for PT-BR (dubbed)
        if (lowerTitle.Contains("pt-br") || lowerTitle.Contains("ptbr") || 
            lowerTitle.Contains("dublado") || lowerTitle.Contains("dub"))
            return MediaLanguage.PtBr;

        return MediaLanguage.Unknown;
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
        var yearMatch = YearRegex.Match(item.Title);
        if (yearMatch.Success)
            parts.Add($"({yearMatch.Value})");

        // Add resolution
        if (!string.IsNullOrWhiteSpace(item.Resolution))
            parts.Add($"[{item.Resolution}]");

        // Add language tag
        var languageTag = item.Language switch
        {
            MediaLanguage.PtBr => "[PT-BR]",
            MediaLanguage.Dual => "[DUAL]",
            MediaLanguage.Legendado => "[LEG]",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(languageTag))
            parts.Add(languageTag);

        // Add format (BluRay, WEB-DL, etc.) if detected
        var format = DetectFormat(item.Title);
        if (!string.IsNullOrWhiteSpace(format))
            parts.Add($"[{format}]");

        return string.Join(" ", parts).Trim();
    }

    private static string DetectFormat(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var lowerTitle = title.ToLowerInvariant();

        if (lowerTitle.Contains("bluray") || lowerTitle.Contains("bdrip"))
            return "BluRay";
        if (lowerTitle.Contains("web-dl") || lowerTitle.Contains("webdl"))
            return "WEB-DL";
        if (lowerTitle.Contains("webrip") || lowerTitle.Contains("web-rip"))
            return "WEBRip";
        if (lowerTitle.Contains("dvdrip"))
            return "DVDRip";
        if (lowerTitle.Contains("hdtv"))
            return "HDTV";

        return string.Empty;
    }

    private static readonly string[] GarbageTerms =
    {
        "dual áudio", "torrent", "dublado", "dual", "legendado",
        "download", "web-dl", "bluray", "brrip", "hdrip",
        "x264", "x265", "h264", "h265"
    };

    public static string CleanTitleForTmdb(string rawTitle)
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

    public string GenerateSceneReleaseName(MediaItem item)
    {
        // 1 base: título original (EN) se existir
        var baseTitle = !string.IsNullOrWhiteSpace(item.Title)
            ? item.Title
            : item.NormalizedTitle;

        baseTitle = baseTitle
            .Replace(":", "")
            .Replace("-", "")
            .Replace("'", "")
            .Trim();

        baseTitle = Regex.Replace(baseTitle, @"\s+", ".");

        var parts = new List<string>
    {
        baseTitle
    };

        // 2 ano
        parts.Add(item.PublishDate.Year.ToString());

        // 3 resolução
        if (!string.IsNullOrWhiteSpace(item.Resolution))
            parts.Add(item.Resolution);

        // 4 source
        parts.Add("WEB-DL");

        // 5 idioma
        if (item.Language == MediaLanguage.Dual)
            parts.Add("DUAL");
        else if (item.Language == MediaLanguage.PtBr)
            parts.Add("PTBR");
        else if (item.Language == MediaLanguage.Legendado)
            parts.Add("LEG");

        return string.Join(".", parts);
    }
}

