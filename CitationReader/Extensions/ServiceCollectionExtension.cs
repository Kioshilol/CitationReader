using System.Net;
using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Managers.Huur.Auth;
using CitationReader.Managers.Huur.Vehicle;
using CitationReader.Providers.Cache;
using CitationReader.Services.Citation;
using CitationReader.Services.Huur.Auth;
using CitationReader.Services.Huur.Vehicle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CitationReader.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection RegisterCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HuurOptions>(configuration.GetSection("Huur"));

        services.AddHttpClient(HttpClientType.Auth.ToString());
        services.AddHttpClient(HttpClientType.HuurApi.ToString());
        services
            .AddHttpClient(HttpClientType.CitationReader.ToString())
            .ConfigureHttpClient((_, httpClient) =>
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36"
                );
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                httpClient.DefaultRequestHeaders.Referrer = new Uri("https://site.metropolis.io/");
                httpClient.DefaultRequestHeaders.Add("Origin", "https://site.metropolis.io");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var cookieJar = new CookieContainer();
                return new HttpClientHandler
                {
                    UseCookies = true,
                    CookieContainer = cookieJar,
                    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
                };
            });
        

        // Register memory cache
        services.AddMemoryCache();

        // Register logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        RegisterProviders(services);
        RegisterManagers(services);
        RegisterServices(services);

        return services;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<ICitationService, CitationService>();
        
        services.AddAllCitationReaders();
    }
    
    private static void RegisterManagers(IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IVehicleManager, VehicleManager>();
    }
    
    private static void RegisterProviders(IServiceCollection services)
    {
        services.AddScoped<ITokenCacheProvider, TokenCacheProvider>();
    }
}
