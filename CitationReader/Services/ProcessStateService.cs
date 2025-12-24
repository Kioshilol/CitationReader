using System.Collections.Concurrent;
using CitationReader.Models;

namespace CitationReader.Services;

public class ProcessStateService : IProcessStateService
{
    private readonly ConcurrentQueue<LogEntry> _logEntries = new();
    private readonly object _stateLock = new();
    private bool _isProcessRunning;
    private DateTime? _processStartTime;
    private int _processedVehicles;
    private int _totalVehicles;
    private int _violationCount;

    public bool IsProcessRunning
    {
        get
        {
            lock (_stateLock)
            {
                return _isProcessRunning;
            }
        }
    }

    public DateTime? ProcessStartTime
    {
        get
        {
            lock (_stateLock)
            {
                return _processStartTime;
            }
        }
    }

    public int ProcessedVehicles
    {
        get
        {
            lock (_stateLock)
            {
                return _processedVehicles;
            }
        }
    }

    public int TotalVehicles
    {
        get
        {
            lock (_stateLock)
            {
                return _totalVehicles;
            }
        }
    }

    public int ViolationCount
    {
        get
        {
            lock (_stateLock)
            {
                return _violationCount;
            }
        }
    }

    public void StartProcess()
    {
        lock (_stateLock)
        {
            _isProcessRunning = true;
            _processStartTime = DateTime.Now;
        }
    }

    public void StopProcess()
    {
        lock (_stateLock)
        {
            _isProcessRunning = false;
            _processStartTime = null; // Reset start time to clear duration
        }
    }

    public void SetTotalVehicles(int total)
    {
        lock (_stateLock)
        {
            _totalVehicles = total;
        }
    }

    public void IncrementProcessedVehicles()
    {
        lock (_stateLock)
        {
            _processedVehicles++;
        }
    }

    public void IncrementViolationCount()
    {
        lock (_stateLock)
        {
            _violationCount++;
        }
    }

    public void ResetProgress()
    {
        lock (_stateLock)
        {
            _processedVehicles = 0;
            _totalVehicles = 0;
            _violationCount = 0;
        }
    }

    public void AddLogEntry(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        _logEntries.Enqueue(entry);
    }

    public List<LogEntry> GetLogEntries()
    {
        return _logEntries.ToList();
    }

    public void ClearLogs()
    {
        while (_logEntries.TryDequeue(out _)) { }
    }
}
