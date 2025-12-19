using CitationReader.Models.Base;
using CitationReader.Models.Huur;

namespace CitationReader.Managers.Huur.Violations;

public interface IViolationManager
{
    Task<BaseResponse<ParkingViolation>> CreateParkingViolationAsync(ParkingViolation parkingViolation);
}