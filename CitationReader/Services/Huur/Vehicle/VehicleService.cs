using System.Collections.Generic;
using System.Threading.Tasks;
using CitationReader.Managers.Huur.Vehicle;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;

namespace CitationReader.Services.Huur.Vehicle
{
    public class VehicleService : IVehicleService
    {
        private readonly IVehicleManager _vehicleManager;

        public VehicleService(IVehicleManager vehicleManager)
        {
            _vehicleManager = vehicleManager;
        }

        public Task<BaseResponse<IEnumerable<ExternalVehicleDto>>> GetExternalVehiclesAsync()
        {
            return _vehicleManager.GetExternalVehiclesAsync();
        }
    }
}
