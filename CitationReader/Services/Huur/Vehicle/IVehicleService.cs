using System.Collections.Generic;
using System.Threading.Tasks;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;

namespace CitationReader.Services.Huur.Vehicle
{
    public interface IVehicleService
    {
        Task<BaseResponse<IEnumerable<ExternalVehicleDto>>> GetExternalVehiclesAsync();
    }
}
