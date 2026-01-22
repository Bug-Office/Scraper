# Quick Start Guide

## Build and Run

### Local Development

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
cd src/Scraper.Api
dotnet run
```

The API will be available at `http://localhost:9898`

### Docker

```bash
# Build and run
docker-compose up -d

# View logs
docker logs -f media-scraper

# Stop
docker-compose down
```

## Test the API

### Health Check
```bash
curl http://localhost:9898/health
```

### Torznab Capabilities
```bash
curl "http://localhost:9898/api?t=caps"
```

### Search
```bash
curl "http://localhost:9898/api?t=search&q=matrix"
```

### Movie Search
```bash
curl "http://localhost:9898/api?t=movie&q=matrix"
curl "http://localhost:9898/api?t=movie&imdbid=tt0133093"
```

### TV Search
```bash
curl "http://localhost:9898/api?t=tvsearch&q=breaking+bad"
```

## Add to Prowlarr

1. Open Prowlarr → Settings → Indexers
2. Click "+" → Select "Custom Torznab"
3. Configure:
   - **Name**: Media Scraper
   - **URL**: `http://your-server:9898/api`
   - **Categories**: `2000` (Movies), `5000` (TV)
4. Test and Save

## Project Structure

```
Scraper/
├── src/
│   ├── Scraper.Core/          # Domain models and interfaces
│   ├── Scraper.Infrastructure/ # Scrapers and HTTP clients
│   └── Scraper.Api/            # Minimal API and Torznab endpoints
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## Adding a New Scraper

1. Create a class inheriting from `BaseScraper`
2. Implement `SearchAsync` method
3. Register in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<IScraper, YourScraper>();
   ```

See `ExampleScraper.cs` for a template.

