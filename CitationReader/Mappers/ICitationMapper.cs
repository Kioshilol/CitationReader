using CitationReader.Models.Citation.Internal;
using CitationReader.Models.Huur;

namespace CitationReader.Mappers;

public interface ICitationMapper
{
    ParkingViolation MapToParkingViolation(CitationModel citation, Dictionary<string, ExternalVehicleDto> vehicleLookup);
}
