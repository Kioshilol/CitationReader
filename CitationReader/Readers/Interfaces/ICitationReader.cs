using CitationReader.Enums;
using CitationReader.Models.Citation;
using CitationReader.Models.Huur;

namespace CitationReader.Readers.Interfaces;

public interface ICitationReader
{
    CitationType SupportedType { get; }
    
    string Link { get; }

    Task<BaseCitationResponse<IEnumerable<CitationDto>>> ReadCitationsWithResponseAsync(
        string licensePlate,
        string state);
}