# Apache Torrent Scraper

## Overview

The `ApacheTorrentScraper` is a production-ready scraper implementation for [Apache Torrent](https://apachetorrent.com), a Brazilian torrent site that specializes in PT-BR, DUAL, and LEGENDADO releases.

## Features

- ✅ **Search Support**: Searches movies and series using the site's search API
- ✅ **Language Detection**: Automatically detects PT-BR, DUAL, and LEGENDADO releases
- ✅ **Resolution Detection**: Extracts resolution information (720p, 1080p, 2160p)
- ✅ **Detail Page Scraping**: Automatically follows detail pages to extract magnet/torrent links
- ✅ **Media Type Detection**: Distinguishes between movies and TV series
- ✅ **Error Handling**: Robust error handling with logging

## Search URL Format

The scraper uses the following search URL format:
```
https://apachetorrent.com/index.php?s={query}
```

Example: `https://apachetorrent.com/index.php?s=five+nights`

## How It Works

1. **Search**: The scraper sends a search request to Apache Torrent's search page
2. **Parse Results**: Extracts titles, links, and metadata from the search results
3. **Follow Detail Pages**: If no direct magnet/torrent link is found on the search page, it follows the detail page link to extract the actual download link
4. **Normalize**: Uses the `TitleNormalizer` to clean titles and detect language/resolution
5. **Return Results**: Returns a list of `MediaItem` objects ready for Torznab API

## HTML Structure Handling

The scraper handles multiple HTML structures:

- `<article>` tags
- `<div>` with classes containing "post", "item", or "torrent"
- `<h2>` and `<h3>` headings with links
- Direct anchor tags with PHP links

## Language Detection

The scraper detects language from titles and text:
- **PT-BR**: "Dublado", "PT-BR", "PTBR"
- **DUAL**: "Dual", "Duplo", "Dual Áudio"
- **LEGENDADO**: "Legendado", "LEG", "Sub"

## Media Type Detection

Automatically determines if content is a movie or TV series based on:
- Series indicators: "S01E01", "Season", "Temporada", "Série"
- Movie indicators: "Filme"
- Defaults to Movie if uncertain

## Configuration

The scraper is registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<IScraper, ApacheTorrentScraper>();
```

To disable the scraper, set `IsEnabled` to `false` in the scraper class.

## Performance Considerations

- **Parallel Processing**: Results are parsed in parallel for better performance
- **Caching**: Results are cached for 10 minutes to avoid redundant requests
- **Detail Page Fetching**: Only fetches detail pages when necessary (no direct link found)

## Error Handling

The scraper includes comprehensive error handling:
- Network errors are logged and the scraper continues with other results
- Parsing errors for individual items don't stop the entire search
- Failed detail page fetches fall back to using the detail page URL

## Testing

Test the scraper using the Torznab API:

```bash
# Search for movies
curl "http://localhost:9898/api?t=search&q=five+nights"

# Search for TV shows
curl "http://localhost:9898/api?t=tvsearch&q=breaking+bad"
```

## Notes

- The scraper respects rate limits and includes proper User-Agent headers
- If Cloudflare protection is detected, consider integrating FlareSolverr (see `FLARESOLVERR.md`)
- The scraper may need adjustments if Apache Torrent changes their HTML structure

