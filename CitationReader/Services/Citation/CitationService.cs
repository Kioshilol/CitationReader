using CitationReader.Services.Huur.Vehicle;
using CitationReader.Enums;
using CitationReader.Models.Huur;
using CitationReader.Readers.Interfaces;
using CitationReader.Services.Huur.Auth;
using CitationReader.Services.Huur.Violations;
using CitationReader.Mappers;
using System.Collections.Concurrent;
using CitationReader.Models.Citation.Internal;

namespace CitationReader.Services.Citation;

public class CitationService : ICitationService
{
    private readonly ILogger<CitationService> _logger;
    private readonly IVehicleService _vehicleService;
    private readonly IAuthService _authService;
    private readonly IViolationService _violationService;
    private readonly ICitationMapper _citationMapper;
    private readonly Dictionary<CitationProviderType, ICitationReader> _readers;

    public CitationService(
        IEnumerable<ICitationReader> readers,
        IVehicleService vehicleService,
        IAuthService authService,
        IViolationService violationService,
        ICitationMapper citationMapper,
        ILogger<CitationService> logger)
    {
        _logger = logger;
        _vehicleService = vehicleService;
        _authService = authService;
        _violationService = violationService;
        _citationMapper = citationMapper;
        _readers = readers.ToDictionary(r => r.SupportedProviderType, r => r);
    }

    public async Task<IEnumerable<CitationModel>> ReadAllCitations()
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting to read citations for all external vehicles");
        
        var allCitations = new ConcurrentBag<CitationModel>();
        var allErrors = new ConcurrentBag<CitationError>();
        
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
                .GetValues<CitationProviderType>()
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
            
            var citationsList = allCitations.ToList();
            var errorsList = allErrors.ToList();

            if (citationsList.Count <= 0)
            {
                return null;
            }
            
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;
            
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
                    .GroupBy(e => e.CitationProviderType)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                foreach (var providerErrors in errorsByProvider)
                {
                    _logger.LogWarning(
                        "Provider {Provider} had {ErrorCount} errors", 
                        providerErrors.Key, 
                        providerErrors.Value);
                }
            }
            
            var citationsByProvider = citationsList
                .GroupBy(c => c.CitationProviderType)
                .ToDictionary(g => g.Key, g => g.Count());
            
            foreach (var providerCitations in citationsByProvider)
            {
                _logger.LogInformation(
                    "Found {CitationCount} citations from {Provider}", 
                    providerCitations.Value,
                    providerCitations.Key);
            }
            
            // Send citations to server
            await SendCitationsToServerAsync(citationsList, vehicles);
            
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
        CitationProviderType citationProviderProvider,
        ConcurrentBag<CitationModel> allCitations,
        ConcurrentBag<CitationError> allErrors,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        
        try
        {
            if (!_readers.TryGetValue(citationProviderProvider, out var reader))
            {
                _logger.LogWarning("No reader found for citationProviderProvider {Provider}", citationProviderProvider);
                return;
            }
            
            _logger.LogDebug(
                "Processing vehicle {Tag} ({State}) with citationProviderProvider {Provider}", 
                vehicle.Tag,
                vehicle.State, 
                citationProviderProvider);
            
            var response = await reader.ReadCitationsAsync(
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
                        citationProviderProvider);
                }
            }
            
            if (response.Error != null)
            {
                allErrors.Add(response.Error);
                
                _logger.LogWarning(
                    "Error reading citations for vehicle {Tag} from {Provider}: {Error}", 
                    vehicle.Tag,
                    citationProviderProvider, 
                    response.Error.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Exception reading citations for vehicle {Tag} from citationProviderProvider {Provider}", 
                vehicle.Tag,
                citationProviderProvider);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    private async Task SendCitationsToServerAsync(List<CitationModel> citations, ExternalVehicleDto[] vehicles)
    {
        if (!citations.Any())
        {
            _logger.LogInformation("No citations to send to server");
            return;
        }
        
        _logger.LogInformation("Starting to send {CitationCount} citations to server", citations.Count);
        
        // Create a lookup dictionary for vehicles by tag and state for efficient matching
        var vehicleLookup = vehicles.ToDictionary(v => $"{v.Tag}_{v.State}", v => v);
        
        var successCount = 0;
        var failureCount = 0;
        var sendingTasks = new List<Task>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent server calls
        
        foreach (var citation in citations)
        {
            var task = SendSingleCitationAsync(citation, vehicleLookup, semaphore, 
                () => Interlocked.Increment(ref successCount),
                () => Interlocked.Increment(ref failureCount));
            sendingTasks.Add(task);
        }
        
        await Task.WhenAll(sendingTasks);
        
        _logger.LogInformation(
            "Completed sending citations to server. Success: {SuccessCount}, Failures: {FailureCount}", 
            successCount, 
            failureCount);
    }
    
    private async Task SendSingleCitationAsync(
        CitationModel citation, 
        Dictionary<string, ExternalVehicleDto> vehicleLookup,
        SemaphoreSlim semaphore,
        Action onSuccess,
        Action onFailure)
    {
        await semaphore.WaitAsync();
        
        try
        {
            var parkingViolation = _citationMapper.MapToParkingViolation(citation, vehicleLookup);
            var response = await _violationService.CreateParkingViolationAsync(parkingViolation);
            if (response.IsSuccess)
            {
                _logger.LogDebug(
                    "Successfully sent citation {CitationNumber} from {Provider} to server", 
                    citation.CitationNumber, 
                    citation.CitationProviderType);
                onSuccess();
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send citation {CitationNumber} from {Provider} to server. Error: {Error}", 
                    citation.CitationNumber, 
                    citation.CitationProviderType,
                    response.Message);
                onFailure();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Exception occurred while sending citation {CitationNumber} from {Provider} to server", 
                citation.CitationNumber, 
                citation.CitationProviderType);
            onFailure();
        }
        finally
        {
            semaphore.Release();
        }
    }
    
}
