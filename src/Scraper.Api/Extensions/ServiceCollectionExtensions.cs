using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Core.Interfaces;
using Scraper.Core.Normalizers;
using Scraper.Infrastructure.Data;
using Scraper.Infrastructure.Interfaces;
using Scraper.Infrastructure.Repositories;
using Scraper.Infrastructure.Services;
using Scraper.Api.Services;
using System.Net.Http.Headers;

namespace Scraper.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScraperServices(this IServiceCollection services, string dbPath)
    {
        // Configure SQLite database
        services.AddDbContext<ScraperDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Add HTTP context accessor for URL generation
        services.AddHttpContextAccessor();

        // Get data directory path for default scrapers JSON
        var dataDir = Path.GetDirectoryName(dbPath) ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var defaultScrapersPath = Path.Combine(dataDir, "default-scrapers.json");

        // Register core services
        services.AddSingleton<ITitleNormalizer, TitleNormalizer>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<Scraper.Infrastructure.Services.ScraperConfigService>();
        services.AddScoped<Scraper.Infrastructure.Services.ScraperConfigurationService>(sp =>
        {
            var scraperConfigService = sp.GetRequiredService<Scraper.Infrastructure.Services.ScraperConfigService>();
            var logger = sp.GetRequiredService<ILogger<Scraper.Infrastructure.Services.ScraperConfigurationService>>();
            return new Scraper.Infrastructure.Services.ScraperConfigurationService(scraperConfigService, logger, defaultScrapersPath);
        });
        services.AddScoped<DynamicScraperService>();
        services.AddScoped<ScraperInitializationService>();
        services.AddScoped<Scraper.Infrastructure.Services.ScraperMigrationService>();

        // Register TMDB Service
        services.AddHttpClient<ITmdbService, TmdbService>(client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Register Services
        services.AddScoped<TorznabService>();
        services.AddScoped<IFlareSolverrService, FlareSolverrService>();
        services.AddScoped<IScraperService, ScraperService>(sp =>
        {
            var dynamicScraperService = sp.GetRequiredService<DynamicScraperService>();
            var scraperConfigService = sp.GetRequiredService<Scraper.Infrastructure.Services.ScraperConfigService>();
            var logger = sp.GetRequiredService<ILogger<ScraperService>>();
            return new ScraperService(dynamicScraperService, scraperConfigService, logger);
        });

        // Register Repositories
        services.AddScoped<IMediaItemRepository, MediaItemRepository>();

        return services;
    }
}

