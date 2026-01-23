using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Core.Interfaces;
using Scraper.Core.Normalizers;
using Scraper.Infrastructure.Data;
using Scraper.Infrastructure.Interfaces;
using Scraper.Infrastructure.Repositories;
using Scraper.Infrastructure.Scrapers;
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

        // Register core services
        services.AddSingleton<ITitleNormalizer, TitleNormalizer>();
        services.AddScoped<IConfigurationService, ConfigurationService>();

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
        services.AddScoped<IScraperService, ScraperService>();

        // Register Repositories
        services.AddScoped<IMediaItemRepository, MediaItemRepository>();

        // Register Scrapers
        services.AddScoped<IScraper, ApacheTorrentScraper>();        

        return services;
    }
}

