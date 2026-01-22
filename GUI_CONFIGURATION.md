# GUI Configuration Guide

The Media Scraper now includes a web-based configuration interface similar to Jackett, allowing you to configure scrapers and settings through a user-friendly web UI.

## Accessing the Configuration UI

1. Start the application (via Docker or directly)
2. Open your web browser and navigate to:
   ```
   http://localhost:9898
   ```
   or
   ```
   http://<your-server-ip>:9898
   ```

The UI will automatically load and display the current configuration.

## Features

### General Settings

- **Cache Expiration**: Set how long (in minutes) cached search results should be stored
- **Cache Sliding Expiration**: Set the sliding expiration time for cache entries
- **FlareSolverr URL**: Optional URL for FlareSolverr integration (for Cloudflare-protected sites)

### Scraper Configuration

For each scraper, you can:

1. **Enable/Disable**: Toggle scrapers on or off using the switch
2. **Configure Settings**:
   - **Base URL**: Override the default base URL for the scraper
   - **Cookies**: Add authentication cookies in JSON format
     ```json
     {
       "session_id": "your-session-id",
       "token": "your-token"
     }
     ```
   - **Custom Headers**: Add custom HTTP headers in JSON format
     ```json
     {
       "X-Custom-Header": "value",
       "User-Agent": "Custom User Agent"
     }
     ```
   - **Search URL Pattern**: (Apache Torrent only) Customize the search URL pattern

### Actions

- **💾 Save Configuration**: Saves all changes to the configuration file
- **🔄 Reload**: Reloads the current configuration from disk
- **🧪 Test Connection**: Tests the connection to all enabled scrapers

## Configuration Storage

Configuration is stored in:
```
config/appsettings.json
```

This file is automatically created when you first save configuration. The file is in JSON format and can be manually edited if needed.

## Example Configuration

```json
{
  "scrapers": [
    {
      "name": "ApacheTorrent",
      "isEnabled": true,
      "settings": {
        "baseUrl": "https://apachetorrent.com",
        "cookies": {
          "session_id": "abc123"
        },
        "headers": {
          "User-Agent": "Mozilla/5.0"
        }
      }
    }
  ],
  "cacheExpirationMinutes": 10,
  "cacheSlidingExpirationMinutes": 5,
  "flareSolverrUrl": null
}
```

## API Endpoints

The configuration UI uses the following API endpoints:

- `GET /api/config` - Get current configuration
- `POST /api/config` - Save configuration
- `GET /api/scrapers` - List available scrapers
- `GET /api/test` - Test connection to scrapers

## Notes

- Configuration changes require an application restart to take full effect
- The UI automatically validates JSON input for cookies and headers
- Invalid JSON will show an error message when saving
- The configuration file is created in the `config` directory relative to the application

## Troubleshooting

### UI Not Loading

- Ensure the application is running
- Check that port 9898 is accessible
- Verify static files are being served (check browser console for errors)

### Configuration Not Saving

- Check file permissions on the `config` directory
- Verify JSON format is valid
- Check application logs for errors

### Scrapers Not Appearing

- Ensure scrapers are registered in `Program.cs`
- Check that scrapers implement `IScraper` interface
- Verify the scraper's `Name` property is set correctly

