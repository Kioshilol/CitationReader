using CitationReader.Enums;
using CitationReader.Models.Huur;

namespace CitationReader.Readers.Interfaces;

public interface ICitationReader
{
    CitationType SupportedType { get; }
    
    Task<IEnumerable<CitationDto>> ReadCitationsAsync(string licensePlate, string state);
}