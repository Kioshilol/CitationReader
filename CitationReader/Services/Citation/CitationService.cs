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
    private readonly IProcessStateService _processStateService;
    private readonly Dictionary<CitationProviderType, ICitationReader> _readers;
    
    // Provider-specific rate limiting
    private static readonly Dictionary<CitationProviderType, SemaphoreSlim> ProviderSemaphores = new();
    private static readonly Dictionary<CitationProviderType, DateTime> ProviderLastRequest = new();
    private static readonly Dictionary<CitationProviderType, object> ProviderLocks = new();
    
    private static readonly Dictionary<CitationProviderType, int> ProviderDelays = new()
    {
        { CitationProviderType.Metropolis, 200 },
        { CitationProviderType.Vanguard, 200 },
        { CitationProviderType.ProfessionalParkingManagement, 400 },
        { CitationProviderType.CityOfFortLauderdale, 100 },
        { CitationProviderType.MiamiParking, 500 },
        { CitationProviderType.ParkingCompliance, 500 },
        { CitationProviderType.CityOfKeyWest, 500 },
    };
    
    private static readonly Dictionary<CitationProviderType, int> ProviderConcurrency = new()
    {
        { CitationProviderType.Metropolis, 10 },
        { CitationProviderType.Vanguard, 10 },       
        { CitationProviderType.ProfessionalParkingManagement, 10 }, 
        { CitationProviderType.CityOfFortLauderdale, 1 },
        { CitationProviderType.MiamiParking, 10 },
        { CitationProviderType.ParkingCompliance, 10 },
        { CitationProviderType.CityOfKeyWest, 10 },
    };

    public CitationService(
        IEnumerable<ICitationReader> readers,
        IVehicleService vehicleService,
        IAuthService authService,
        IViolationService violationService,
        ICitationMapper citationMapper,
        IProcessStateService processStateService,
        ILogger<CitationService> logger)
    {
        _logger = logger;
        _vehicleService = vehicleService;
        _authService = authService;
        _violationService = violationService;
        _citationMapper = citationMapper;
        _processStateService = processStateService;
        _readers = readers.ToDictionary(r => r.SupportedProviderType, r => r);
        
        // Initialize provider semaphores
        InitializeProviderSemaphores();
    }
    
    private static void InitializeProviderSemaphores()
    {
        foreach (var provider in Enum.GetValues<CitationProviderType>())
        {
            if (!ProviderSemaphores.ContainsKey(provider))
            {
                var concurrency = ProviderConcurrency.GetValueOrDefault(provider, 1);
                ProviderSemaphores[provider] = new SemaphoreSlim(concurrency, concurrency);
                ProviderLastRequest[provider] = DateTime.MinValue;
                ProviderLocks[provider] = new object(); // Каждый провайдер получает свой lock
            }
        }
    }
    
    private async Task ApplyProviderRateLimit(CitationProviderType provider, CancellationToken cancellationToken)
    {
        // Get provider-specific semaphore and timing
        var semaphore = ProviderSemaphores[provider];
        var requiredDelay = ProviderDelays.GetValueOrDefault(provider, 2000);
        
        // Wait for available slot
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Apply timing delay using provider-specific lock
            lock (ProviderLocks[provider])
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - ProviderLastRequest[provider];
                var delayNeeded = Math.Max(0, requiredDelay - (int)timeSinceLastRequest.TotalMilliseconds);
                
                if (delayNeeded > 0)
                {
                    _logger.LogDebug("Provider {Provider} rate limiting: waiting {Delay}ms", provider, delayNeeded);
                    Task.Delay(delayNeeded, cancellationToken).Wait(cancellationToken);
                }
                
                ProviderLastRequest[provider] = DateTime.UtcNow;
            }
        }
        finally
        {
            // Release the semaphore slot after the request is made
            // Note: We'll release this in the calling method after the actual request
        }
    }

    public Task<IEnumerable<CitationModel>> ReadAllCitationsAsync(CancellationToken cancellationToken = default)
    {
        var allProviders = Enum.GetValues<CitationProviderType>().Where(p => _readers.ContainsKey(p));
        return ReadCitationsFromProvidersAsync(allProviders, cancellationToken);
    }

    public async Task<IEnumerable<CitationModel>> ReadCitationsByProviderAndPlateNumberAsync(
        CitationProviderType provider, 
        string licensePlate, 
        string state,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting citation lookup for plate '{LicensePlate}' in state '{State}' using provider '{Provider}'", licensePlate, state, provider);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!_readers.TryGetValue(provider, out var reader))
            {
                _logger.LogWarning("No reader found for provider {Provider}", provider);
                return [];
            }
            
            _logger.LogDebug("Processing plate {LicensePlate} ({State}) with provider {Provider}", licensePlate, state, provider);
            
            var response = await reader.ReadCitationsAsync(licensePlate, state);
            if (response is { IsSuccess: true, Data: not null })
            {
                var citations = response.Data.ToList();
                _logger.LogInformation("Found {Count} citations for plate {LicensePlate} from {Provider}", citations.Count, licensePlate, provider);
                return citations;
            }
            
            if (response.Error != null)
            {
                _logger.LogWarning("Error reading citations for plate {LicensePlate} from {Provider}: {Error}", licensePlate, provider, response.Error.Message);
            }
            
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception reading citations for plate {LicensePlate} from provider {Provider}", licensePlate, provider);
            return [];
        }
    }

    public async Task<IEnumerable<CitationModel>> ReadCitationsFromProvidersAsync(
        IEnumerable<CitationProviderType> providers, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var selectedProviders = providers.ToList();
        _logger.LogInformation("Starting to read citations for selected providers: {Providers}", string.Join(", ", selectedProviders));
        
        var allCitations = new ConcurrentBag<CitationModel>();
        var allErrors = new ConcurrentBag<CitationError>();
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Attempting authorization...");
            var isAuthSuccess = await _authService.TryAuthorizeAsync();
            if (!isAuthSuccess)
            {
                const string fatalError = "Fatal error: Authorization failed. Cannot proceed with citation reading.";
                _logger.LogCritical(fatalError);
                throw new InvalidOperationException(fatalError);
            }
            
            _logger.LogInformation("Authorization successful");
            
            cancellationToken.ThrowIfCancellationRequested();
            
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

            //TODO: only for FL state
            vehicles = vehicles
                .Where(v => v.State == "FL")
                .ToArray();
            
            var availableProviders = selectedProviders
                .Where(p => _readers.ContainsKey(p))
                .ToList();
            
            _logger.LogInformation(
                "Found {VehicleCount} external vehicles and {ProviderCount} citation providers", 
                vehicles.Length,
                availableProviders.Count);
            
            // Set up progress tracking with vehicle count
            _processStateService.SetTotalVehicles(vehicles.Length);
            _processStateService.ResetProgress();
            
            cancellationToken.ThrowIfCancellationRequested();
            
            var processingTasks = new List<Task>();
            var semaphore = new SemaphoreSlim(100); // Allow high concurrency for bulk processing - provider-specific limits will control individual providers
            
            var processedVehicles = new ConcurrentDictionary<string, bool>();
            
            foreach (var vehicle in vehicles)
            {
                foreach (var provider in availableProviders)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var task = ProcessVehicleProviderAsync(
                        vehicle,
                        provider, 
                        allCitations,
                        allErrors,
                        semaphore,
                        processedVehicles,
                        cancellationToken);
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
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Send citations to server
            await SendCitationsToServerAsync(citationsList, vehicles, cancellationToken);
            
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
        SemaphoreSlim semaphore,
        ConcurrentDictionary<string, bool> processedVehicles,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
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
            
            // Apply provider-specific rate limiting
            await ApplyProviderRateLimit(citationProviderProvider, cancellationToken);
            
            BaseCitationResult<IEnumerable<CitationModel>> response;
            try
            {
                response = await reader.ReadCitationsAsync(vehicle.Tag, vehicle.State);
            }
            finally
            {
                // Release the provider semaphore after the request
                ProviderSemaphores[citationProviderProvider].Release();
            }
            
            if (response is { IsSuccess: true, Data: not null })
            {
                var citations = response.Data.ToList();
                foreach (var citation in citations)
                {
                    allCitations.Add(citation);
                    _processStateService.IncrementViolationCount();
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
            
            // Track vehicle progress (only increment once per vehicle, not per provider)
            var vehicleKey = $"{vehicle.Tag}_{vehicle.State}";
            if (processedVehicles.TryAdd(vehicleKey, true))
            {
                _processStateService.IncrementProcessedVehicles();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Exception reading citations for vehicle {Tag} from citationProviderProvider {Provider}", 
                vehicle.Tag,
                citationProviderProvider);
            
            // Make sure to release provider semaphore on exception
            try
            {
                ProviderSemaphores[citationProviderProvider].Release();
            }
            catch
            {
                // Ignore release errors
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    private async Task SendCitationsToServerAsync(
        List<CitationModel> citations,
        ExternalVehicleDto[] vehicles, 
        CancellationToken cancellationToken)
    {
        if (!citations.Any())
        {
            _logger.LogInformation("No citations to send to server");
            return;
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Starting to send {CitationCount} citations to server", citations.Count);
        
        // Create a lookup dictionary for vehicles by tag and state for efficient matching
        var vehicleLookup = vehicles.ToDictionary(v => $"{v.Tag}_{v.State}", v => v);
        
        var successCount = 0;
        var failureCount = 0;
        var sendingTasks = new List<Task>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent server calls
        
        foreach (var citation in citations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var task = SendSingleCitationAsync(citation, vehicleLookup, semaphore, 
                () => Interlocked.Increment(ref successCount),
                () => Interlocked.Increment(ref failureCount),
                cancellationToken);
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
        Action onFailure,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
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
