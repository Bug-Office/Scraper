# Media Scraper - Torznab API

A production-ready .NET 8 application that scrapes HTML-based media websites and exposes a Torznab-compatible API for integration with Prowlarr, Radarr, and Sonarr.

## Features

- 🎨 **Web Configuration UI**: Jackett-like web interface for configuring scrapers and settings
- 🔍 **HTML Scraping**: Pluggable scraper architecture for multiple websites
- 🌐 **Torznab API**: Full Torznab specification compliance
- 🇧🇷 **PT-BR Support**: Normalizes and tags PT-BR, DUAL, and LEGENDADO releases
- ⚡ **Caching**: In-memory caching to reduce redundant requests
- 📦 **Docker Ready**: Complete Docker and docker-compose setup
- 🏗️ **Clean Architecture**: Separation of concerns with Core, Infrastructure, and API layers
- 📝 **Structured Logging**: Serilog integration for comprehensive logging

## Architecture

```
Scraper.Core/
  ├── Interfaces/          # Core interfaces (IScraper, IScraperService, etc.)
  ├── Models/              # Domain models (MediaItem, SearchRequest)
  └── Normalizers/         # Title normalization and language detection

Scraper.Infrastructure/
  ├── Http/                # HTTP client factories
  ├── Scrapers/            # Scraper implementations (BaseScraper, ExampleScraper)
  └── Services/            # Infrastructure services

Scraper.Api/
  ├── Models/              # Torznab XML models
  ├── Services/            # Torznab service and XML serialization
  └── Program.cs           # Minimal API setup
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker (optional, for containerized deployment)

### Local Development

1. **Clone and restore dependencies:**
   ```bash
   dotnet restore
   ```

2. **Build the solution:**
   ```bash
   dotnet build
   ```

3. **Run the API:**
   ```bash
   cd src/Scraper.Api
   dotnet run
   ```

   The API will be available at `http://localhost:9898`
   - **Configuration UI**: `http://localhost:9898`
   - **Torznab API**: `http://localhost:9898/api`

### Docker Deployment

1. **Build and run with Docker Compose:**
   ```bash
   docker-compose up -d
   ```

2. **Or build and run manually:**
   ```bash
   docker build -t media-scraper .
   docker run -p 9898:9898 media-scraper
   ```

3. **Or use pre-built image from GitHub Container Registry:**
   ```bash
   docker pull ghcr.io/<your-username>/<repository-name>:latest
   docker run -p 9898:9898 ghcr.io/<your-username>/<repository-name>:latest
   ```

## CI/CD

This project includes GitHub Actions workflows for automated building and Docker image creation. See [GITHUB_ACTIONS.md](GITHUB_ACTIONS.md) for details.

**Available workflows:**
- **CI**: Builds on every push/PR
- **Build and Test**: Comprehensive build with tests
- **Docker Build**: Builds and pushes Docker images to GHCR
- **Release**: Creates release artifacts and Docker images

## Configuration UI

The application includes a web-based configuration interface (similar to Jackett) accessible at `http://localhost:9898`. 

### Features

- **Enable/Disable Scrapers**: Toggle scrapers on or off
- **Configure Settings**: Set base URLs, cookies, headers, and other scraper-specific options
- **Cache Configuration**: Adjust cache expiration times
- **Test Connection**: Verify scrapers are working correctly

### Accessing the UI

1. Start the application
2. Open your browser to `http://localhost:9898`
3. Configure your scrapers and settings
4. Click "Save Configuration"
5. Restart the application for changes to take effect

See [GUI_CONFIGURATION.md](GUI_CONFIGURATION.md) for detailed documentation.

## API Endpoints

### Torznab Endpoints

All endpoints follow the Torznab specification:

- **Search**: `/api?t=search&q={query}`
- **Movie Search**: `/api?t=movie&q={query}` or `/api?t=movie&imdbid={imdbId}`
- **TV Search**: `/api?t=tvsearch&q={query}&season={season}&episode={episode}`
- **Capabilities**: `/api?t=caps`

### Health Check

- **Health**: `GET /health`

## Adding to Prowlarr

1. **Open Prowlarr** → Settings → Indexers

2. **Click "+" (Add Indexer)** → Select **"Custom Torznab"**

3. **Configure the indexer:**
   - **Name**: Media Scraper (or your preferred name)
   - **URL**: `http://your-server:9898/api`
   - **API Key**: Leave empty (not required for this implementation)
   - **Categories**: 
     - `2000` (Movies)
     - `5000` (TV Shows)

4. **Test the connection** and save

5. **Sync with Radarr/Sonarr** as needed

## Creating a New Scraper

To add support for a new website, create a new scraper class:

1. **Create a new scraper class** inheriting from `BaseScraper`:

```csharp
public class MyTrackerScraper : BaseScraper
{
    public MyTrackerScraper(
        ITitleNormalizer titleNormalizer,
        ILogger<MyTrackerScraper> logger)
        : base(
            HttpClientFactory.CreateClient("https://mytracker.com"),
            titleNormalizer,
            logger)
    {
    }

    public override string Name => "MyTracker";
    public override bool IsEnabled => true;

    public override async Task<IEnumerable<MediaItem>> SearchAsync(
        SearchRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Implement your scraping logic here
        // Use FetchHtmlAsync() to get HTML
        // Use ParseHtml() to parse it
        // Use CreateMediaItem() to create results
    }
}
```

2. **Register the scraper** in `Program.cs`:

```csharp
builder.Services.AddSingleton<IScraper, MyTrackerScraper>();
```

3. **Handle authentication** if needed:

```csharp
var client = HttpClientFactory.CreateClient(
    baseUrl: "https://mytracker.com",
    cookies: new Dictionary<string, string>
    {
        { "session_id", "your-session-id" }
    }
);
```

## Cloudflare Protection

If a website uses Cloudflare protection, you can integrate with FlareSolverr:

1. **Set up FlareSolverr** (separate service)
2. **Modify your scraper** to use FlareSolverr as a proxy:

```csharp
// Example: Configure HttpClient to use FlareSolverr proxy
var handler = new HttpClientHandler
{
    Proxy = new WebProxy("http://flaresolverr:8191")
};
```

## Normalization

The `TitleNormalizer` automatically:

- **Detects Language**: PT-BR, DUAL, LEGENDADO
- **Detects Resolution**: 720p, 1080p, 2160p
- **Normalizes Titles**: Removes prefixes, suffixes, and file extensions
- **Generates Safe Release Names**: Scene-compatible format for Radarr/Sonarr

## Caching

Results are cached in-memory for 10 minutes (absolute) or 5 minutes (sliding expiration) to reduce redundant scraping requests.

## Logging

Structured logging is provided by Serilog. Logs include:

- Scraper search operations
- Parsing errors
- API requests
- Cache hits/misses

## Configuration

Edit `appsettings.json` to configure:

- Log levels
- Cache expiration times
- Scraper enable/disable flags

## License

This project is provided as-is for educational and personal use.

## Contributing

To add new scrapers:

1. Follow the `BaseScraper` pattern
2. Implement proper error handling
3. Add logging for debugging
4. Test with real queries
5. Update this README if needed

## Troubleshooting

### No results returned

- Check scraper `IsEnabled` property
- Verify HTML selectors match the website structure
- Check logs for parsing errors
- Ensure the website is accessible

### Torznab validation fails

- Verify XML structure matches Torznab spec
- Check that required fields (title, link, guid) are populated
- Ensure categories are set correctly (2000 for movies, 5000 for TV)

### Docker container won't start

- Check port 9898 is not in use
- Verify Docker has sufficient resources
- Check container logs: `docker logs media-scraper`

### Configuration UI not loading

- Ensure static files are being served
- Check browser console for errors
- Verify the `wwwroot` directory exists with `index.html`

## Future Enhancements

- [ ] Database persistence for cache
- [ ] Multiple authentication methods
- [ ] Rate limiting per scraper
- [ ] Metrics and monitoring endpoints

