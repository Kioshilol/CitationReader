using CitationReader.Logging;

namespace CitationReader.Providers.Logging;

public class CustomLoggerProvider : ILoggerProvider
{
    private readonly Action<LogLevel, string> _logAction;

    public CustomLoggerProvider(Action<LogLevel, string> logAction)
    {
        _logAction = logAction;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CustomLogger(_logAction, categoryName);
    }

    public void Dispose() { }
}
