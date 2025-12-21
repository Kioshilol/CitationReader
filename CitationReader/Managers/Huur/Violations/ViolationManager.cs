using CitationReader.Enums;
using CitationReader.Managers.Base;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;
using CitationReader.Providers.Cache;

namespace CitationReader.Managers.Huur.Violations;

public class ViolationManager : BaseHttpManager, IViolationManager
{
    private readonly ITokenCacheProvider _tokenCacheProvider;

    public ViolationManager(
        ITokenCacheProvider tokenCacheProvider) 
        : base(HttpClientType.HuurApi)
    {
        _tokenCacheProvider = tokenCacheProvider;
    }
    
    public Task<BaseResponse<ParkingViolation>> CreateParkingViolationAsync(ParkingViolation parkingViolation)
    {
        Logger.LogInformation($"Create Parking Violation for {parkingViolation.Tag} {parkingViolation.State}");
        
        var token = _tokenCacheProvider.GetCachedToken();
        return RequestAsync<ParkingViolation>(
            HttpMethod.Post,
            "ExternalViolation/create",
            parkingViolation,
            token,
            CreateSuccessResponseCallback);
        
        bool CreateSuccessResponseCallback(BaseResponse<ParkingViolation> baseResponse)
        {
            var errorMessage = baseResponse.Message ?? "";
            return errorMessage.Contains("Violation already exists") ||
                   errorMessage.Contains("Violation already exists for") || 
                   errorMessage.Contains("VIOLATION_EXISTS");
        }
    }
}