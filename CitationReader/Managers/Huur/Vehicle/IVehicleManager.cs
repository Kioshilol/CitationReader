using CitationReader.Models.Base;
using CitationReader.Models.Huur;

namespace CitationReader.Managers.Huur.Vehicle
{
    public interface IVehicleManager
    {
        Task<BaseResponse<IEnumerable<ExternalVehicleDto>>> GetExternalVehiclesAsync();
    }
}
