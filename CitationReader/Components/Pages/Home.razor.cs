using CitationReader.Services.Citation;
using CitationReader.Services;
using CitationReader.Enums;
using CitationReader.Models;
using CitationReader.Models.Citation.Internal;
using CitationReader.Providers.Logging;
using CitationReader.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CitationReader.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] 
    private ICitationService CitationService { get; set; }
    
    [Inject] 
    private IProcessStateService ProcessStateService { get; set; }
    
    [Inject] 
    private IJSRuntime JsRuntime { get; set; }
    
    [Inject] 
    private IWebHostEnvironment Environment { get; set; }

    private bool _isStarting;
    private bool _isStopping;
    private bool _autoScroll = true;
    private CancellationTokenSource? _cancellationTokenSource;
    private Timer? _uiUpdateTimer;
    private Task? _runningTask;
    private CitationProviderType? _selectedProvider;
    private readonly List<CitationProviderType> _availableProviders = Enum.GetValues<CitationProviderType>().ToList();
    
    // Citation Lookup Modal properties
    private bool _showCitationLookupModal;
    private CitationProviderType? _lookupProvider;
    private string _lookupPlateNumber = string.Empty;
    private bool _isLookingUp;

    protected override void OnInitialized()
    {
        _uiUpdateTimer = new Timer(
            async _ => await InvokeAsync(StateHasChanged),
            null, 
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            // Check if process was already running when page loads
            if (ProcessStateService.IsProcessRunning)
            {
                AddLogEntry(LogLevel.Information, "🔄 Reconnected to running process...");
                StateHasChanged();
            }
        }
    }

    private async Task StartProcess()
    {
        if (ProcessStateService.IsProcessRunning || _isStarting)
        {
            return;
        }

        _isStarting = true;
        
        // Ensure any previous task is fully stopped and cleaned up
        if (_runningTask != null)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                await _runningTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (Exception ex)
            {
                AddLogEntry(LogLevel.Warning, $"Previous task cleanup: {ex.Message}");
            }
        }
        
        // Dispose old cancellation token and create a new one
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        
        ProcessStateService.StartProcess();
        ProcessStateService.ResetProgress();
        StateHasChanged();

        try
        {
            AddLogEntry(LogLevel.Information, "🚀 Starting citation reading process...");
            
            // In development environment, hook into global logging to capture ALL logs
            if (Environment.IsDevelopment())
            {
                AddLogEntry(LogLevel.Information, "🔧 Development mode: Capturing ALL application logs");
                
                // Add our custom logger provider to the global logging system
                var loggerFactory = Program.ServiceProvider.GetService<ILoggerFactory>();
                if (loggerFactory != null)
                {
                    loggerFactory.AddProvider(new CustomLoggerProvider(AddLogEntry));
                    AddLogEntry(LogLevel.Information, "📡 Global log capture enabled - you'll see logs from all files");
                }
            }
            else
            {
                AddLogEntry(LogLevel.Information, "🏭 Production mode: Limited logging enabled");
            }
            
            await Task.Delay(500, _cancellationTokenSource.Token); // Small delay to show starting state
            
            _isStarting = false;
            StateHasChanged();

            _runningTask = Task.Run(async () =>
            {
                try
                {
                    IEnumerable<CitationModel> result;
                    
                    if (_selectedProvider.HasValue)
                    {
                        AddLogEntry(LogLevel.Information, $"📋 Running for selected provider: {GetProviderDisplayName(_selectedProvider.Value)}");
                        result = await CitationService.ReadCitationsFromProvidersAsync(new[] { _selectedProvider.Value }, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        AddLogEntry(LogLevel.Information, "📋 Running for all available providers");
                        result = await CitationService.ReadAllCitationsAsync(_cancellationTokenSource.Token);
                    }
                    
                    if (result != null)
                    {
                        var citationCount = result.Count();
                        AddLogEntry(LogLevel.Information, $"✅ Process completed successfully! Found {citationCount} citations.");
                    }
                    else
                    {
                        AddLogEntry(LogLevel.Information, "✅ Process completed successfully! No citations found.");
                    }
                }
                catch (OperationCanceledException)
                {
                    AddLogEntry(LogLevel.Warning, "⚠️ Process was cancelled by user.");
                }
                catch (Exception ex)
                {
                    AddLogEntry(LogLevel.Error, $"❌ Process failed: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        AddLogEntry(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
                    }
                }
                finally
                {
                    ProcessStateService.StopProcess();
                    await InvokeAsync(StateHasChanged);
                }
            }, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            AddLogEntry(LogLevel.Warning, "⚠️ Process was cancelled.");
            ProcessStateService.StopProcess();
        }
        catch (Exception ex)
        {
            AddLogEntry(LogLevel.Error, $"❌ Failed to start process: {ex.Message}");
            ProcessStateService.StopProcess();
        }
        finally
        {
            _isStarting = false;
            StateHasChanged();
        }
    }

    private async Task StopProcess()
    {
        if (!ProcessStateService.IsProcessRunning || _isStopping) return;

        _isStopping = true;
        StateHasChanged();

        AddLogEntry(LogLevel.Warning, "🛑 Stopping process...");
        _cancellationTokenSource?.Cancel();
        
        // Wait for the running task to complete before marking as stopped
        if (_runningTask != null)
        {
            try
            {
                await _runningTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (Exception ex)
            {
                AddLogEntry(LogLevel.Error, $"Error during process stop: {ex.Message}");
            }
        }
        
        ProcessStateService.StopProcess();
        _isStopping = false;
        StateHasChanged();
    }

    private void ClearLogs()
    {
        ProcessStateService.ClearLogs();
        StateHasChanged();
    }

    private async void AddLogEntry(LogLevel level, string message)
    {
        ProcessStateService.AddLogEntry(level, message);
        await InvokeAsync(StateHasChanged);

        if (_autoScroll)
        {
            // Delay the scroll to ensure DOM is updated
            await InvokeAsync(async () =>
            {
                await Task.Delay(100); // Increased delay to ensure DOM update
                try
                {
                    await JsRuntime.InvokeVoidAsync("scrollToBottom", "logContainer");
                }
                catch
                {
                    // Ignore JS errors
                }
            });
        }
    }

    private string GetLogLevelClass(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "log-trace",
            LogLevel.Debug => "log-debug",
            LogLevel.Information => "log-info",
            LogLevel.Warning => "log-warning",
            LogLevel.Error => "log-error",
            LogLevel.Critical => "log-critical",
            _ => "log-info"
        };
    }

    private string GetDurationString()
    {
        var startTime = ProcessStateService.ProcessStartTime;
        if (startTime == null) return "00:00";
        
        var duration = DateTime.Now - startTime.Value;
        return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    private List<LogEntry> GetLogEntriesForDisplay()
    {
        return ProcessStateService.GetLogEntries();
    }

    private bool IsRunning => ProcessStateService.IsProcessRunning;

    private static string GetProviderDisplayName(CitationProviderType provider)
    {
        return provider.GetDisplayName();
    }

    // Citation Lookup Modal methods
    private void OpenCitationLookupModal()
    {
        _showCitationLookupModal = true;
        _lookupProvider = null;
        _lookupPlateNumber = string.Empty;
        StateHasChanged();
    }

    private void CloseCitationLookupModal()
    {
        _showCitationLookupModal = false;
        _lookupProvider = null;
        _lookupPlateNumber = string.Empty;
        _isLookingUp = false;
        StateHasChanged();
    }

    private void FormatPlateNumber(ChangeEventArgs e)
    {
        var input = e.Value?.ToString()?.ToUpperInvariant() ?? string.Empty;
        // Remove any non-alphanumeric characters
        _lookupPlateNumber = new string(input.Where(char.IsLetterOrDigit).ToArray());
        StateHasChanged();
    }

    private async Task PerformCitationLookup()
    {
        if (!_lookupProvider.HasValue || string.IsNullOrWhiteSpace(_lookupPlateNumber))
        {
            return;
        }

        _isLookingUp = true;
        StateHasChanged();

        try
        {
            AddLogEntry(LogLevel.Information, $"🔍 Starting citation lookup for plate '{_lookupPlateNumber}' using {GetProviderDisplayName(_lookupProvider.Value)}...");
            
            // Create a temporary cancellation token for this lookup
            using var lookupCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            
            // Use the new method to look up citations for the specific plate and provider
            var result = await CitationService.ReadCitationsByProviderAndPlateNumberAsync(
                _lookupProvider.Value, 
                _lookupPlateNumber,
                "FL", // Default state, could be made configurable later
                lookupCancellationTokenSource.Token);
            
            if (result != null && result.Any())
            {
                var citationCount = result.Count();
                AddLogEntry(LogLevel.Information, $"✅ Citation lookup completed! Found {citationCount} citation(s) for plate '{_lookupPlateNumber}'.");
                
                // Log details of found citations
                foreach (var citation in result)
                {
                    var citationNumber = citation.CitationNumber ?? citation.NoticeNumber ?? "Unknown";
                    var fineTypeText = ((FineType)citation.FineType).ToString();
                    AddLogEntry(LogLevel.Information, $"📋 Citation found: {citationNumber} - {fineTypeText} (${citation.Amount})");
                }
            }
            else
            {
                AddLogEntry(LogLevel.Information, $"ℹ️ No citations found for plate '{_lookupPlateNumber}' using {GetProviderDisplayName(_lookupProvider.Value)}.");
            }
        }
        catch (OperationCanceledException)
        {
            AddLogEntry(LogLevel.Warning, $"⚠️ Citation lookup for plate '{_lookupPlateNumber}' was cancelled (timeout).");
        }
        catch (Exception ex)
        {
            AddLogEntry(LogLevel.Error, $"❌ Citation lookup failed for plate '{_lookupPlateNumber}': {ex.Message}");
        }
        finally
        {
            _isLookingUp = false;
            CloseCitationLookupModal();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _uiUpdateTimer?.Dispose();
    }
}
