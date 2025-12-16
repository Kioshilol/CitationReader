using CitationReader.Models.Huur;

namespace CitationReader.Services.Citation;

public interface ICitationService
{
    Task<IEnumerable<CitationDto>> ReadAllCitations();
}
