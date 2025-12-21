using System.Net;
using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Managers.Huur.Auth;
using CitationReader.Managers.Huur.Vehicle;
using CitationReader.Managers.Huur.Violations;
using CitationReader.Providers.Cache;
using CitationReader.Services.Citation;
using CitationReader.Services.Huur.Auth;
using CitationReader.Services.Huur.Vehicle;
using CitationReader.Services.Huur.Violations;
using CitationReader.Mappers;
using CitationReader.Services;

namespace CitationReader.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection RegisterCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HuurOptions>(configuration.GetSection("Huur"));

        services.AddHttpClient(HttpClientType.Auth.ToString());
        services.AddHttpClient(HttpClientType.HuurApi.ToString());
        services.AddHttpClient(HttpClientType.ParseCitationReader.ToString())
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
        services
            .AddHttpClient(HttpClientType.HttpCitationReader.ToString())
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
        services
            .AddHttpClient(HttpClientType.ParseHostedCitationReader.ToString())
            .ConfigureHttpClient((_, httpClient) =>
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
                httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
                httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
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

        RegisterMappers(services);
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
        services.AddScoped<IViolationService, ViolationService>();
        
        // Register as singleton to persist state across page refreshes
        services.AddSingleton<IProcessStateService, ProcessStateService>();
        
        services.AddAllCitationReaders();
    }
    
    private static void RegisterManagers(IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IVehicleManager, VehicleManager>();
        services.AddScoped<IViolationManager, ViolationManager>();
    }
    
    private static void RegisterProviders(IServiceCollection services)
    {
        services.AddScoped<ITokenCacheProvider, TokenCacheProvider>();
    }
    
    private static void RegisterMappers(IServiceCollection services)
    {
        services.AddScoped<ICitationMapper, CitationMapper>();
    }
}
