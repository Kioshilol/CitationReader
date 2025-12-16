using CitationReader.Services.Huur.Vehicle;
using CitationReader.Enums;
using CitationReader.Models.Huur;
using CitationReader.Readers.Interfaces;
using CitationReader.Services.Huur.Auth;

namespace CitationReader.Services.Citation;

public class CitationService : ICitationService
{
    private readonly ILogger<CitationService> _logger;
    private readonly IVehicleService _vehicleService;
    private readonly IAuthService _authService;
    private readonly Dictionary<CitationType, ICitationReader> _readers;

    public CitationService(
        IEnumerable<ICitationReader> readers,
        IVehicleService vehicleService,
        IAuthService authService,
        ILogger<CitationService> logger)
    {
        _logger = logger;
        _vehicleService = vehicleService;
        _authService = authService;
        _readers = readers.ToDictionary(r => r.SupportedType, r => r);
    }

    public async Task<IEnumerable<CitationDto>> ReadAllCitations()
    {
        _logger.LogInformation("Starting to read citations for all external vehicles");
        
        var allCitations = new List<CitationDto>();
        
        try
        {
            var isSuccess = await _authService.TrySignInAsync();
            if (!isSuccess)
            {
                return null;
            }
            
            var vehiclesResponse = await _vehicleService.GetExternalVehiclesAsync();
            if (!vehiclesResponse.IsSuccess)
            {
                return null;
            }
            
            _logger.LogInformation("Found {Count} external vehicles", vehiclesResponse.Result.Count());
            
            // Read citations for each vehicle from each provider
            foreach (var vehicle in vehiclesResponse.Result)
            {
                _logger.LogInformation("Reading citations for vehicle: {Tag} ({LicensePlate})", 
                    vehicle.Tag, vehicle.LicensePlate);
                
                foreach (var provider in Enum.GetValues<CitationType>())
                {
                    try
                    {
                        if (_readers.TryGetValue(provider, out var reader))
                        {
                            var citations = await reader.ReadCitationsAsync(vehicle.Tag, vehicle.State);
                            allCitations.AddRange(citations);
                            
                            if (citations.Any())
                            {
                                _logger.LogInformation("Found {Count} citations for vehicle {Tag} from {Provider}", 
                                    citations.Count(), vehicle.Tag, provider);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading citations for vehicle {Tag} from provider {Provider}", 
                            vehicle.Tag, provider);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading citations");
        }
        
        _logger.LogInformation("Completed reading citations. Total: {Total}", allCitations.Count);
        return allCitations;
    }
}