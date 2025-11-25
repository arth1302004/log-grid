using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LogGrid.Client
{
    public class LogArchivalService : BackgroundService
    {
        private readonly LogGridClientConfig _config;
        private readonly ILogger<LogArchivalService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public LogArchivalService(IOptions<LogGridClientConfig> config, ILogger<LogArchivalService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled || !_config.Providers.UseFile)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ArchiveOldLogs();
                    CleanupArchivedLogs();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during log archival process.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private void ArchiveOldLogs()
        {
            var logPath = _config.File.Path;
            // Assuming path format like "../Logs/log-.json" or similar. 
            // We need the directory and the file pattern.
            
            var directory = Path.GetDirectoryName(logPath);
            if (string.IsNullOrEmpty(directory))
            {
                // If path is just a filename, use current directory
                directory = Directory.GetCurrentDirectory();
            }
            else if (!Path.IsPathRooted(directory))
            {
                // Resolve relative path
                directory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), directory));
            }

            if (!Directory.Exists(directory))
            {
                return;
            }

            var archiveDirectory = Path.Combine(directory, "Archive");
            if (!Directory.Exists(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
            }

            var retentionDays = _config.File.RetentionDays;
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);

            // Pattern matching for Serilog rolling files. 
            // Serilog typically appends date like log-20231027.json or log20231027.txt
            // We'll look for files in the directory and try to parse date.
            
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                var fileInfo = new FileInfo(file);

                // Skip if it's a directory
                if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)) continue;

                bool shouldArchive = false;

                // 1. Check if it's a "rolled" file (e.g., log-20251121_001.json)
                // Serilog appends _XXX for rolled files.
                if (Regex.IsMatch(filename, @"_\d{3}\.json$"))
                {
                    shouldArchive = true;
                }
                // 2. Check if it's an old file based on retention days
                else if (TryExtractDate(filename, out DateTime fileDate))
                {
                    if (fileDate.Date < cutoffDate.Date)
                    {
                        shouldArchive = true;
                    }
                }

                if (shouldArchive)
                {
                    var destFile = Path.Combine(archiveDirectory, filename);
                    try
                    {
                        if (File.Exists(destFile))
                        {
                            File.Delete(destFile); // Overwrite if exists
                        }
                        File.Move(file, destFile);
                        _logger.LogInformation($"Archived log file: {filename}");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, $"Failed to move file {filename} to archive. It might be in use.");
                    }
                }
            }
        }

        private void CleanupArchivedLogs()
        {
            var logPath = _config.File.Path;
            var directory = Path.GetDirectoryName(logPath);
             if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }
            else if (!Path.IsPathRooted(directory))
            {
                directory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), directory));
            }

            var archiveDirectory = Path.Combine(directory, "Archive");
            if (!Directory.Exists(archiveDirectory))
            {
                return;
            }

            var archiveRetentionDays = _config.File.ArchiveRetentionDays;
            var cutoffDate = DateTime.Now.AddDays(-archiveRetentionDays);

            var files = Directory.GetFiles(archiveDirectory);
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                if (TryExtractDate(filename, out DateTime fileDate))
                {
                    if (fileDate.Date < cutoffDate.Date)
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.LogInformation($"Deleted old archived log file: {filename}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to delete archived file {filename}");
                        }
                    }
                }
            }
        }

        private bool TryExtractDate(string filename, out DateTime date)
        {
            date = DateTime.MinValue;
            // Regex for yyyyMMdd (Serilog default)
            var match = Regex.Match(filename, @"(\d{4})(\d{2})(\d{2})");
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int year) &&
                    int.TryParse(match.Groups[2].Value, out int month) &&
                    int.TryParse(match.Groups[3].Value, out int day))
                {
                    try
                    {
                        date = new DateTime(year, month, day);
                        return true;
                    }
                    catch { }
                }
            }
            
            // Regex for yyyy-MM-dd
            match = Regex.Match(filename, @"(\d{4})-(\d{2})-(\d{2})");
            if (match.Success)
            {
                 if (int.TryParse(match.Groups[1].Value, out int year) &&
                    int.TryParse(match.Groups[2].Value, out int month) &&
                    int.TryParse(match.Groups[3].Value, out int day))
                {
                    try
                    {
                        date = new DateTime(year, month, day);
                        return true;
                    }
                    catch { }
                }
            }

            return false;
        }
    }
}
