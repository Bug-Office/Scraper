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

### Implementações Genéricas

- **`GenericTorrentMetadataExtractor`**: Extrator genérico para sites de torrent (usado por todos os scrapers)
- **`GenericTorrentLinkExtractor`**: Extrator de links genérico para sites de torrent
- **`GenericTorrentEpisodeExtractor`**: Extrator de episódios genérico para sites de torrent
- **`GenericTorrentDetailPageParser`**: Parser de detalhes genérico para sites de torrent

> **Nota**: Para sites com estruturas muito diferentes, você pode criar implementações específicas herdando das classes base.

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

### 2. Criar o Scraper (usando ConfigurableTorrentScraper)

Para sites com estrutura similar aos sites brasileiros de torrent, você pode usar o `ConfigurableTorrentScraper`:

```csharp
// Em Scrapers/MeuSiteScraper.cs
public class MeuSiteScraper : ConfigurableTorrentScraper
{
    public MeuSiteScraper(
        ITitleNormalizer titleNormalizer,
        ILogger<MeuSiteScraper> logger,
        ITmdbService tmdbService,
        ILoggerFactory loggerFactory,
        IFlareSolverrService? flareSolverrService = null,
        IMediaItemRepository? mediaItemRepository = null)
        : base(
            "MeuSite",
            MeuSiteConfiguration.Create(),
            titleNormalizer,
            logger,
            tmdbService,
            loggerFactory,
            flareSolverrService,
            mediaItemRepository)
    {
    }
}
```

### 3. Criar Extratores Específicos (apenas se necessário)

Se o site tiver uma estrutura muito diferente, você pode criar implementações específicas:

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

E então criar um scraper customizado herdando de `BaseScraper` (veja exemplo completo abaixo).

## Benefícios da Arquitetura Modular

1. **Reutilização**: Classes base podem ser usadas por múltiplos scrapers
2. **Testabilidade**: Cada componente pode ser testado isoladamente
3. **Manutenibilidade**: Mudanças em um componente não afetam outros
4. **Extensibilidade**: Fácil adicionar novos scrapers reutilizando componentes existentes
5. **Separação de Responsabilidades**: Cada classe tem uma responsabilidade específica

## Exemplo Completo

### Exemplo Simples (usando ConfigurableTorrentScraper)

Todos os scrapers são criados dinamicamente através do `DynamicScraperService` usando configurações do banco de dados/JSON. Veja `ConfigurableTorrentScraper.cs` para entender como os scrapers são implementados.

### Exemplo Completo (scraper customizado)

Se precisar de um scraper completamente customizado, veja `ConfigurableTorrentScraper.cs` como referência de como implementar usando os componentes modulares.
