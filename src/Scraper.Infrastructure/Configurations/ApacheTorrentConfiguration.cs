using Scraper.Infrastructure.Configurations;

namespace Scraper.Infrastructure.Configurations;

/// <summary>
/// Configuration for Apache Torrent scraper
/// </summary>
public static class ApacheTorrentConfiguration
{
    public static ScraperConfiguration Create()
    {
        return new ScraperConfiguration
        {
            BaseUrl = "https://apachetorrent.com",
            SearchUrlTemplate = "{BaseUrl}/index.php?s={Query}",
            
            ResultItemSelectors = new List<string>
            {
                "//div[contains(@class, 'capaname')]",
                "//article",
                "//div[contains(@class, 'post')]",
                "//div[contains(@class, 'item')]",
                "//div[contains(@class, 'torrent')]"
            },
            
            TitleLinkSelectors = new List<string>
            {
                ".//h2//a",
                ".//h2/a",
                ".//a[contains(@href, 'apachetorrent.com')]",
                ".//a[contains(@href, '/')]"
            },
            
            DownloadSectionSelectors = new List<string>
            {
                "//div[@id='download']",
                "//div[@id='lista_links']"
            },
            
            EpisodeParagraphSelectors = new List<string>
            {
                ".//p[@class='text-center']",
                ".//p[contains(., 'EPISÓDIO')]"
            },
            
            InfoSectionSelectors = new List<string>
            {
                "//div[contains(@class, 'infos')]/p",
                "//div[contains(@class, 'conteudo')]"
            },
            
            TitleCleanupPatterns = new List<string>
            {
                @"(?i)\s*Torrent\s*Download\s*$",
                @"(?i)\s*Download\s*$",
                @"(?i)\s*Baixar\s*$",
                @"\s*\(Filme de \d{4}\)\s*$",
                @"\s*\(Série de \d{4}\)\s*$"
            }
        };
    }
}
