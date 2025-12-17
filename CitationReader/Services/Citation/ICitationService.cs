using CitationReader.Models.Citation.Internal;
using CitationReader.Models.Huur;

namespace CitationReader.Services.Citation;

public interface ICitationService
{
    Task<IEnumerable<CitationModel>> ReadAllCitations();
}
