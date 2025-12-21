using CitationReader.Models;

namespace CitationReader.Services;

public interface IProcessStateService
{
    bool IsProcessRunning { get; }
    DateTime? ProcessStartTime { get; }
    int ProcessedVehicles { get; }
    int TotalVehicles { get; }
    int ViolationCount { get; }
    void StartProcess();
    void StopProcess();
    void SetTotalVehicles(int total);
    void IncrementProcessedVehicles();
    void IncrementViolationCount();
    void ResetProgress();
    void AddLogEntry(LogLevel level, string message);
    List<LogEntry> GetLogEntries();
    void ClearLogs();
}
