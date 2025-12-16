using CitationReader.Readers.Interfaces;

namespace CitationReader.Extensions
{
    public static class CitationServiceExtensions
    {
        public static IServiceCollection AddAllCitationReaders(this IServiceCollection services)
        {
            var assembly = typeof(ICitationReader).Assembly;
            
            var readerTypes = assembly.GetTypes()
                .Where(type => 
                    type is { IsInterface: false, IsAbstract: false } && 
                    typeof(ICitationReader).IsAssignableFrom(type))
                .ToList();

            foreach (var readerType in readerTypes)
            {
                services.AddScoped(typeof(ICitationReader), readerType);
            }

            return services;
        }
    }
}
