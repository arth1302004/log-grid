using LogGrid.Models;

namespace LogGrid.Services;

public class LoggingService
{
    private readonly IEnumerable<ILogProvider> _providers;

    public LoggingService(IEnumerable<ILogProvider> providers)
    {
        _providers = providers;
    }

    public async Task LogAsync(LogEntry logEntry)
    {
        var tasks = _providers.Select(p => p.LogAsync(logEntry));
        await Task.WhenAll(tasks);
    }
}
