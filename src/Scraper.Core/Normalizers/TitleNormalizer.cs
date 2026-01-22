using System.Text.RegularExpressions;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

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

        return normalized.Trim();
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
}

