using System.Globalization;
using LogGrid.Models;
using Microsoft.Extensions.Options;

namespace LogGrid.Services;

public class LogRetentionService : BackgroundService
{
    private readonly ILogger<LogRetentionService> _logger;
    private readonly IOptionsMonitor<LoggingProviders> _loggingProviders;
    private readonly IHostApplicationLifetime _hostLifetime;

    public LogRetentionService(ILogger<LogRetentionService> logger, IOptionsMonitor<LoggingProviders> loggingProviders, IHostApplicationLifetime hostLifetime)
    {
        _logger = logger;
        _loggingProviders = loggingProviders;
        _hostLifetime = hostLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // When the application starts, wait for it to be fully running
        await WaitForApplicationStarted(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CleanupOldLogFiles();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while cleaning up old log files.");
            }

            // Wait for 24 hours before running again
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private void CleanupOldLogFiles()
    {
        var retentionDays = _loggingProviders.CurrentValue.File.RetentionDays;
        if (retentionDays <= 0)
        {
            _logger.LogInformation("Log file retention is disabled (RetentionDays set to {RetentionDays}).", retentionDays);
            return;
        }

        _logger.LogInformation("Starting cleanup of log files older than {RetentionDays} days.", retentionDays);

        var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logDirectory))
        {
            _logger.LogWarning("Log directory '{LogDirectory}' not found. Nothing to clean up.", logDirectory);
            return;
        }

        var files = Directory.GetFiles(logDirectory, "*.json");
        var deletedCount = 0;

        foreach (var file in files)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                {
                    if (fileDate < DateTime.UtcNow.Date.AddDays(-retentionDays))
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogInformation("Deleted old log file: {FileName}", Path.GetFileName(file));
                    }
                }
                else
                {
                    _logger.LogWarning("Could not parse date from log file name: {FileName}", Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete log file: {FileName}", Path.GetFileName(file));
            }
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation("Log cleanup complete. Deleted {DeletedCount} file(s).", deletedCount);
        }
        else
        {
            _logger.LogInformation("No old log files to delete.");
        }
    }
    
    private Task WaitForApplicationStarted(CancellationToken stoppingToken)
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _hostLifetime.ApplicationStarted.Register(() => completionSource.TrySetResult());
        return completionSource.Task;
    }
}
