namespace CitationReader.Logging;

public class CustomLogger : ILogger
{
    private readonly Action<LogLevel, string> _logAction;
    private readonly string _categoryName;

    public CustomLogger(Action<LogLevel, string> logAction, string categoryName)
    {
        _logAction = logAction;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
        var formattedMessage = $"[{categoryShort}] {message}";
        
        if (exception != null)
        {
            formattedMessage += $" | Exception: {exception.Message}";
        }
        
        _logAction(logLevel, formattedMessage);
    }
}
