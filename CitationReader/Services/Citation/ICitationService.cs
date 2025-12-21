using CitationReader.Models.Citation.Internal;
using CitationReader.Models.Huur;
using CitationReader.Enums;

namespace CitationReader.Services.Citation;

public interface ICitationService
{
    Task<IEnumerable<CitationModel>> ReadAllCitationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CitationModel>> ReadCitationsFromProvidersAsync(IEnumerable<CitationProviderType> providers, CancellationToken cancellationToken = default);
    Task<IEnumerable<CitationModel>> ReadCitationsByProviderAndPlateNumberAsync(CitationProviderType provider, string licensePlate, string state = "NY", CancellationToken cancellationToken = default);
}
