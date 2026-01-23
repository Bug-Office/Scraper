# Scraper Parsers - Arquitetura Modular

Esta pasta contém as classes modulares para parsing e extração de dados de sites de torrent. A arquitetura foi projetada para ser facilmente replicável para novos scrapers.

## Estrutura

### Interfaces

- **`IDetailPageParser`**: Interface para enriquecer MediaItems com informações de páginas de detalhes
- **`IMetadataExtractor`**: Interface para extrair metadados (tamanho, formato, qualidade, idioma)
- **`ILinkExtractor`**: Interface para extrair links de download (magnet/torrent)
- **`IEpisodeExtractor`**: Interface para extrair episódios de séries

### Classes Base

- **`BaseMetadataExtractor`**: Implementação base com padrões comuns de extração
- **`BaseLinkExtractor`**: Implementação base para extração de links
- **`BaseEpisodeExtractor`**: Implementação base para extração de episódios
- **`BaseDetailPageParser`**: Implementação base para parsing de páginas de detalhes

### Implementações Específicas

- **`ApacheTorrentMetadataExtractor`**: Extrator específico para Apache Torrent
- **`ApacheTorrentLinkExtractor`**: Extrator de links específico para Apache Torrent
- **`ApacheTorrentEpisodeExtractor`**: Extrator de episódios específico para Apache Torrent
- **`ApacheTorrentDetailPageParser`**: Parser de detalhes específico para Apache Torrent

## Como Criar um Novo Scraper

### 1. Criar Configuração

```csharp
// Em Configurations/MeuSiteConfiguration.cs
public static class MeuSiteConfiguration
{
    public static ScraperConfiguration Create()
    {
        return new ScraperConfiguration
        {
            BaseUrl = "https://meusite.com",
            SearchUrlTemplate = "{BaseUrl}/search?q={Query}",
            ResultItemSelectors = new List<string>
            {
                "//div[@class='result-item']",
                "//article"
            },
            // ... outros seletores
        };
    }
}
```

### 2. Criar Extratores Específicos (se necessário)

```csharp
// Em Parsers/MeuSiteMetadataExtractor.cs
public class MeuSiteMetadataExtractor : BaseMetadataExtractor
{
    public override List<MediaLanguage> ExtractLanguages(string? text)
    {
        // Implementar lógica específica do site
        var languages = new List<MediaLanguage>();
        // ... sua lógica aqui
        return languages;
    }
}
```

### 3. Criar o Scraper

```csharp
// Em Scrapers/MeuSiteScraper.cs
public class MeuSiteScraper : BaseScraper
{
    private readonly ScraperConfiguration _configuration;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly ILinkExtractor _linkExtractor;
    private readonly IEpisodeExtractor _episodeExtractor;
    private readonly IDetailPageParser _detailPageParser;

    public MeuSiteScraper(...)
    {
        _configuration = MeuSiteConfiguration.Create();
        _metadataExtractor = new MeuSiteMetadataExtractor();
        _linkExtractor = new BaseLinkExtractor(); // Ou MeuSiteLinkExtractor se necessário
        _episodeExtractor = new BaseEpisodeExtractor(...); // Ou MeuSiteEpisodeExtractor
        _detailPageParser = new BaseDetailPageParser(...); // Ou MeuSiteDetailPageParser
    }

    // Implementar SearchAsync usando os componentes modulares
}
```

## Benefícios da Arquitetura Modular

1. **Reutilização**: Classes base podem ser usadas por múltiplos scrapers
2. **Testabilidade**: Cada componente pode ser testado isoladamente
3. **Manutenibilidade**: Mudanças em um componente não afetam outros
4. **Extensibilidade**: Fácil adicionar novos scrapers reutilizando componentes existentes
5. **Separação de Responsabilidades**: Cada classe tem uma responsabilidade específica

## Exemplo Completo

Veja `ApacheTorrentScraper.cs` como exemplo completo de como usar a arquitetura modular.
