using LogGrid.Models;

namespace LogGrid.Services;

public interface ILogProvider
{
    Task LogAsync(LogEntry logEntry);
}
