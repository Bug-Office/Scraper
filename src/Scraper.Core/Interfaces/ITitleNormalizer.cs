using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface ITitleNormalizer
{
    string NormalizeTitle(string title);
    string NormalizeTitle(MediaItem item);
    MediaLanguageInfo BuildLanguageInfo(List<MediaLanguage> languages);
    List<Models.MediaLanguage> DetectLanguages(string title);
    string DetectResolution(string title);
    string GenerateSafeReleaseName(Models.MediaItem item);
    //string GenerateSceneReleaseName(MediaItem item);
}

