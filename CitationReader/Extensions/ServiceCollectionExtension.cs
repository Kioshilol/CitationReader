using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Managers.Huur.Auth;
using CitationReader.Managers.Huur.Vehicle;
using CitationReader.Providers.Cache;
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
        services.AddHttpClient(HttpClientType.External.ToString());

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
        // Auth services
        services.AddScoped<IAuthService, AuthService>();
        
        // External services
        services.AddScoped<IVehicleService, VehicleService>();
    }
    
    private static void RegisterManagers(IServiceCollection services)
    {
        // Auth managers
        services.AddScoped<IAuthManager, AuthManager>();
        
        // External managers
        services.AddScoped<IVehicleManager, VehicleManager>();
    }
    
    private static void RegisterProviders(IServiceCollection services)
    {
        services.AddScoped<ITokenCacheProvider, TokenCacheProvider>();
    }
}
