using CitationReader.Enums;
using CitationReader.Managers.Base;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;
using CitationReader.Providers.Cache;

namespace CitationReader.Managers.Huur.Vehicle
{
    public class VehicleManager : BaseHttpManager, IVehicleManager
    {
        private readonly ITokenCacheProvider _tokenCacheProvider;

        public VehicleManager(
            ITokenCacheProvider tokenCacheProvider)
            : base(HttpClientType.HuurApi)
        {
            _tokenCacheProvider = tokenCacheProvider;
        }

        public Task<BaseResponse<IEnumerable<ExternalVehicleDto>>> GetExternalVehiclesAsync()
        {
            Logger.LogInformation("Fetching external vehicles from API");
            
            var token = _tokenCacheProvider.GetCachedToken();
            return RequestAsync<IEnumerable<ExternalVehicleDto>>(
                HttpMethod.Get,
                "ExternalVehicles",
                token: token);
        }
    }
}
