using CitationReader.Managers.Huur.Violations;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;

namespace CitationReader.Services.Huur.Violations;

public class ViolationService : IViolationService
{
    private readonly IViolationManager _violationManager;

    public ViolationService(IViolationManager violationManager)
    {
        _violationManager = violationManager;
    }

    public Task<BaseResponse<ParkingViolation>> CreateParkingViolationAsync(ParkingViolation parkingViolation)
    {
        return _violationManager.CreateParkingViolationAsync(parkingViolation);
    }
}