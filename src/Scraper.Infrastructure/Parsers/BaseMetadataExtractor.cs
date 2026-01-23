using System.Text.RegularExpressions;
using Scraper.Core.Models;

namespace Scraper.Infrastructure.Parsers;

/// <summary>
/// Base implementation of IMetadataExtractor with common extraction patterns
/// </summary>
public abstract class BaseMetadataExtractor : IMetadataExtractor
{
    protected static readonly Regex SizePattern = new(@"(\d+[\.,]?\d*)\s*(GB|MB|KB)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    protected static readonly Regex FormatPattern = new(@"Formato\s*:\s*([A-Z0-9]+(?:\s*(?:/|,)\s*[A-Z0-9]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    protected static readonly Regex QualityPattern = new(@"Qualidade[:\s]+([^\n\r]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    protected static readonly Regex ResolutionPattern = new(@"(\d{3,4}p|4K|Full\s*HD|BluRay|HD|SD)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    public abstract List<MediaLanguage> ExtractLanguages(string? text);
}
