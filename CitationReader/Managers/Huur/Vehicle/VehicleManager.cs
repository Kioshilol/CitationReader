using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Managers.Base;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;
using CitationReader.Providers.Cache;
using Microsoft.Extensions.Options;

namespace CitationReader.Managers.Huur.Vehicle
{
    public class VehicleManager : BaseHttpManager, IVehicleManager
    {
        private readonly ITokenCacheProvider _tokenCacheProvider;

        public VehicleManager(
            IHttpClientFactory httpClientFactory,
            IOptions<HuurOptions> options,
            ILogger<VehicleManager> logger,
            ITokenCacheProvider tokenCacheProvider)
            : base(HttpClientType.HuurApi, httpClientFactory, options, logger)
        {
            _tokenCacheProvider = tokenCacheProvider;
        }

        public Task<BaseResponse<IEnumerable<ExternalVehicleDto>>> GetExternalVehiclesAsync()
        {
            _logger.LogInformation("Fetching external vehicles from API");
            
            var token = _tokenCacheProvider.GetCachedToken();
            return RequestAsync<IEnumerable<ExternalVehicleDto>>(
                HttpMethod.Get,
                "ExternalVehicles",
                token: token);
        }
    }
}
