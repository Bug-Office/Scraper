using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface ITitleNormalizer
{
    string NormalizeTitle(string title, Models.MediaType type);
    Models.MediaLanguage DetectLanguage(string title); // Mantido para compatibilidade
    List<Models.MediaLanguage> DetectLanguages(string title);
    string DetectResolution(string title);
    string GenerateSafeReleaseName(Models.MediaItem item);
    //string GenerateSceneReleaseName(MediaItem item);
}

