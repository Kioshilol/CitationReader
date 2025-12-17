using CitationReader.Services.Huur.Vehicle;
using CitationReader.Enums;
using CitationReader.Models.Citation;
using CitationReader.Models.Huur;
using CitationReader.Readers.Interfaces;
using CitationReader.Services.Huur.Auth;
using System.Collections.Concurrent;

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
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting to read citations for all external vehicles");
        
        var allCitations = new ConcurrentBag<CitationDto>();
        var allErrors = new ConcurrentBag<CitationProcessingError>();
        
        try
        {
            _logger.LogInformation("Attempting authorization...");
            var isAuthSuccess = await _authService.TryAuthorizeAsync();
            if (!isAuthSuccess)
            {
                const string fatalError = "Fatal error: Authorization failed. Cannot proceed with citation reading.";
                _logger.LogCritical(fatalError);
                throw new InvalidOperationException(fatalError);
            }
            
            _logger.LogInformation("Authorization successful");
            _logger.LogInformation("Fetching external vehicles...");
            
            var vehiclesResponse = await _vehicleService.GetExternalVehiclesAsync();
            if (!vehiclesResponse.IsSuccess)
            {
                var fatalError = $"Fatal error: Failed to get external vehicles. Error: {vehiclesResponse.Message}";
                _logger.LogCritical(fatalError);
                throw new InvalidOperationException(fatalError);
            }

            var vehicles = vehiclesResponse.Result?.ToArray();
            if (vehicles is null || !vehicles.Any())
            {
                const string fatalError = $"Fatal error: External vehicles list is empty";
                _logger.LogCritical(fatalError);
                throw new InvalidOperationException(fatalError);
            }
            
            var availableProviders = Enum
                .GetValues<CitationType>()
                .Where(p => _readers.ContainsKey(p))
                .ToList();
            
            _logger.LogInformation(
                "Found {VehicleCount} external vehicles and {ProviderCount} citation providers", 
                vehicles.Length,
                availableProviders.Count);
            
            var processingTasks = new List<Task>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2); // Limit concurrent operations
            
            foreach (var vehicle in vehicles)
            {
                foreach (var provider in availableProviders)
                {
                    var task = ProcessVehicleProviderAsync(
                        vehicle,
                        provider, 
                        allCitations,
                        allErrors,
                        semaphore);
                    processingTasks.Add(task);
                }
            }
            
            _logger.LogInformation(
                "Starting parallel processing of {TaskCount} vehicle-provider combinations", 
                processingTasks.Count);
            
            await Task.WhenAll(processingTasks);
            
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;
            
            var citationsList = allCitations.ToList();
            var errorsList = allErrors.ToList();
            
            _logger.LogInformation(
                "Citation processing completed. Duration: {Duration:mm\\:ss}, " +
                "Citations: {CitationCount}, Errors: {ErrorCount}, " +
                "Vehicles: {VehicleCount}, Providers: {ProviderCount}",
                duration, 
                citationsList.Count,
                errorsList.Count,
                vehicles.Length, 
                availableProviders.Count);
            
            if (errorsList.Any())
            {
                var errorsByProvider = errorsList
                    .GroupBy(e => e.Provider)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                foreach (var providerErrors in errorsByProvider)
                {
                    _logger.LogWarning(
                        "Provider {Provider} had {ErrorCount} errors", 
                        providerErrors.Key, 
                        providerErrors.Value);
                }
            }
            
            var citationsByProvider = citationsList.GroupBy(c => c.Agency)
                .ToDictionary(g => g.Key, g => g.Count());
            
            foreach (var providerCitations in citationsByProvider)
            {
                _logger.LogInformation("Found {CitationCount} citations from {Provider}", 
                    providerCitations.Value,
                    providerCitations.Key);
            }
            
            return citationsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error occurred during citation processing");
            throw; // Re-throw fatal errors
        }
    }
    
    private async Task ProcessVehicleProviderAsync(
        ExternalVehicleDto vehicle,
        CitationType provider,
        ConcurrentBag<CitationDto> allCitations,
        ConcurrentBag<CitationProcessingError> allErrors,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        
        try
        {
            if (!_readers.TryGetValue(provider, out var reader))
            {
                _logger.LogWarning("No reader found for provider {Provider}", provider);
                return;
            }
            
            _logger.LogDebug(
                "Processing vehicle {Tag} ({State}) with provider {Provider}", 
                vehicle.Tag,
                vehicle.State, 
                provider);
            
            var response = await reader.ReadCitationsWithResponseAsync(
                vehicle.Tag,
                vehicle.State);
            if (response is { IsSuccess: true, Data: not null })
            {
                var citations = response.Data.ToList();
                foreach (var citation in citations)
                {
                    allCitations.Add(citation);
                }
                
                if (citations.Any())
                {
                    _logger.LogDebug(
                        "Found {Count} citations for vehicle {Tag} from {Provider}", 
                        citations.Count,
                        vehicle.Tag,
                        provider);
                }
            }
            
            if (response.Error != null)
            {
                var error = CitationProcessingError.FromCitationError(
                    vehicle.Tag,
                    vehicle.State,
                    provider, 
                    response.Error);
                allErrors.Add(error);
                
                _logger.LogWarning(
                    "Error reading citations for vehicle {Tag} from {Provider}: {Error}", 
                    vehicle.Tag,
                    provider, 
                    response.Error.Message);
            }
        }
        catch (Exception ex)
        {
            var error = CitationProcessingError.FromException(
                vehicle.Tag,
                vehicle.State,
                provider,
                ex);
            allErrors.Add(error);
            
            _logger.LogError(
                ex, 
                "Exception reading citations for vehicle {Tag} from provider {Provider}", 
                vehicle.Tag,
                provider);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
