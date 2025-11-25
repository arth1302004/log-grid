// LogGrid.Client/LogGridSerilogExtensions.cs
using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Json;

using Serilog.Sinks.PeriodicBatching;

namespace LogGrid.Client
{
    /// <summary>
    /// Serilog fluent extension to register LogGrid sinks (file, ELK, and the custom HTTP batched sink).
    /// Target: LoggerConfiguration to enable fluent chaining: new LoggerConfiguration().LogGrid(configuration)...
    /// </summary>
    public static class LogGridSerilogExtensions
    {
        public static LoggerConfiguration LogGrid(this LoggerConfiguration loggerConfiguration, IConfiguration configuration)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));

            var logGridConfig = configuration?.GetSection("LogGridClient").Get<LogGridClientConfig>() ?? new LogGridClientConfig();

            if (!logGridConfig.Enabled)
                return loggerConfiguration;
            
            // File sink (optional)
            if (logGridConfig.Providers?.UseFile == true)
            {
                var basePath = logGridConfig.File.Path ?? "logs"; // Ensure a default path if not configured

                if (string.Equals(logGridConfig.File.OutputStructure, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var jsonFilePath = Path.Combine(basePath, "log-.json");
                    loggerConfiguration.WriteTo.File(
                        new JsonFormatter(),
                        jsonFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: logGridConfig.File.RetentionDays
                    );
                }
                else
                {
                    var textFilePath = Path.Combine(basePath, "log-.txt");
                    loggerConfiguration.WriteTo.File(
                        textFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: logGridConfig.File.RetentionDays,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                    );
                }
            }



            // Custom LogGrid HTTP batched sink
            if (!string.IsNullOrEmpty(logGridConfig.ApiUrl))
            {
                // Build batching options from config
                var batchingOptions = new PeriodicBatchingSinkOptions
                {
                    BatchSizeLimit = Math.Max(1, logGridConfig.BatchSize),
                    Period = TimeSpan.FromSeconds(Math.Max(1, logGridConfig.BatchPeriodSeconds)),
                    EagerlyEmitFirstEvent = true
                };

                // NOTE: For production prefer providing HttpClient via DI/IHttpClientFactory -> here we create a simple HttpClient for convenience
                var httpClient = new HttpClient();

                var logGridSink = new LogGridSink(httpClient, logGridConfig.ApiUrl);
                var periodicBatchingSink = new PeriodicBatchingSink(logGridSink, batchingOptions);


                loggerConfiguration.WriteTo.Sink(periodicBatchingSink, LogEventLevel.Information);
            }

            return loggerConfiguration;
        }
    }
}
