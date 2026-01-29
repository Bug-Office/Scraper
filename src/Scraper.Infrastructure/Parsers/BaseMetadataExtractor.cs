using System.Text.RegularExpressions;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of IMetadataExtractor with common extraction patterns
/// </summary>
public class BaseMetadataExtractor : IMetadataExtractor
{
    private static readonly Regex SizePattern = new(@"(\d+[\.,]?\d*)\s*(GB|MB|KB)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FormatPattern = new(@"Formato\s*:\s*([A-Z0-9]+(?:\s*(?:/|,)\s*[A-Z0-9]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex QualityPattern = new(@"Qualidade[:\s]+([^\n\r]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ResolutionPattern = new(@"(\d{3,4}p|4K|Full\s*HD|BluRay|HD|SD)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EpisodeNumberPattern = new(@"(?<start>\d{1,2})[º°]?(?:\s*(?:E|A)\s*(?<end>\d{1,2})[º°]?)?\s*EPIS[ÓO]DIO", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SeasonEpisodePattern = new(@"S(?<season>\d{1,2})E(?<start>\d{1,2})(?:-(?<end>\d{1,2}))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SeasonPattern = new(@"(\d{1,2})[ªa]\s*Temporada", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YearRegex = new(@"\(Filme de (\d{4})\)|\(Série de (\d{4})\)", RegexOptions.Compiled);

    public virtual string? ExtractSize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = SizePattern.Match(text);
        if (match.Success)
        {
            return $"{match.Groups[1].Value.Replace(',', '.')} {match.Groups[2].Value.ToUpper()}";
        }

        return null;
    }

    public virtual string? ExtractFormat(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = FormatPattern.Match(text);
        if (match.Success)
        {
            var raw = match.Groups[1].Value;
            var formats = raw
                .Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            return formats.Any() ? string.Join(" / ", formats) : null;
        }

        // Fallback: look for common formats in text
        var commonFormats = new[] { "MKV", "AVI", "MP4", "MPEG", "MOV", "WMV", "FLV" };
        foreach (var format in commonFormats)
        {
            if (text.Contains(format, StringComparison.OrdinalIgnoreCase))
                return format;
        }

        return null;
    }

    public virtual string? ExtractQuality(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = QualityPattern.Match(text);
        if (match.Success)
        {
            var quality = match.Groups[1].Value.Trim();
            quality = Regex.Replace(quality, @"\s*/\s*", " / ", RegexOptions.IgnoreCase);
            return quality;
        }

        // Fallback: look for resolution patterns
        var resolutionMatch = ResolutionPattern.Match(text);
        if (resolutionMatch.Success)
        {
            return resolutionMatch.Groups[1].Value;
        }

        return null;
    }

    public List<MediaLanguage> ExtractLanguages(string? text)
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

    public string? ExtractEpisodeNumber(string text)
    {
        var match = EpisodeNumberPattern.Match(text);

        if (match.Success)
        {
            var start = match.Groups["start"].Value;
            var end = match.Groups["end"].Success ? match.Groups["end"].Value : null;

            if (end != null)
                return $"{start} a {end}";
            else
                return start;
        }
        return null;
    }

    public (string? Season, string? Episode) ExtractSeasonEpisodeFromMagnet(string magnet)
    {
        var match = SeasonEpisodePattern.Match(magnet);

        if (match.Success)
        {
            var season = match.Groups["season"].Value;
            var startEpisode = match.Groups["start"].Value;
            var endEpisode = match.Groups["end"].Success
                ? match.Groups["end"].Value
                : null;

            if (endEpisode != null)
                return (season, $"{startEpisode}-{endEpisode}");
            else
                return (season, startEpisode);
        }
        return (null, null);
    }

    public string? ExtractDateFromText(string text)
    {
        // Look for year in "Filme de YYYY" or "Série de YYYY"
        var yearMatch = YearRegex.Match(text);
        if (yearMatch.Success)
        {
            var year = yearMatch.Groups[1].Success ? yearMatch.Groups[1].Value : yearMatch.Groups[2].Value;
            return $"{year}-01-01";
        }
        return null;
    }

    public string ExtractSeasonFromTitle(string seriesTitle)
    {
        var match = SeasonPattern.Match(seriesTitle);
        return match.Success ? match.Groups[1].Value : "1";
    }
}
