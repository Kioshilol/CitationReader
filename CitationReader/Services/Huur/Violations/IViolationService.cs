using CitationReader.Models.Base;
using CitationReader.Models.Huur;

namespace CitationReader.Services.Huur.Violations;

public interface IViolationService
{
    Task<BaseResponse<ParkingViolation>> CreateParkingViolationAsync(ParkingViolation parkingViolation);
}