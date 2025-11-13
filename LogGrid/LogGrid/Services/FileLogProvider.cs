using System.Text.Json;
using LogGrid.Models;
using Microsoft.Extensions.Options;

namespace LogGrid.Services
{
    public class FileLogProvider : ILogProvider
    {
        private readonly IOptionsMonitor<LoggingProviders> _loggingProviders;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public FileLogProvider(IOptionsMonitor<LoggingProviders> loggingProviders)
        {
            _loggingProviders = loggingProviders;
        }

        public async Task LogAsync(LogEntry logEntry)
        {
            if (!_loggingProviders.CurrentValue.UseFile)
            {
                return;
            }

            await _semaphore.WaitAsync();
            try
            {
                var fileSettings = _loggingProviders.CurrentValue.File;
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), fileSettings.Path);
                Directory.CreateDirectory(logDirectory);

                var fileName = fileSettings.OutputFormat.ToLower() == "json" 
                    ? $"{DateTime.UtcNow:yyyy-MM-dd}.json" 
                    : $"{DateTime.UtcNow:yyyy-MM-dd}.txt";
                
                var logFilePath = Path.Combine(logDirectory, fileName);

                string logString;
                if (fileSettings.OutputFormat.ToLower() == "json")
                {
                    logString = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    logString = $"{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{logEntry.Level}] {logEntry.Application} - {logEntry.Message}";
                    if (logEntry.Properties != null && logEntry.Properties.Count > 0)
                    {
                        logString += $" | Properties: {JsonSerializer.Serialize(logEntry.Properties)}";
                    }
                }

                await File.AppendAllTextAsync(logFilePath, logString + Environment.NewLine);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
