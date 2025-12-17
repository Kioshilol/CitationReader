using CitationReader.Enums;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;
using CitationReader.Models.Huur;

namespace CitationReader.Readers.Interfaces;

public interface ICitationReader
{
    CitationProviderType SupportedProviderType { get; }
    
    string Link { get; }

    Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsWithResponseAsync(
        string licensePlate,
        string state);
}