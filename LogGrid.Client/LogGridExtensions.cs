using LogGrid.Client.Formatting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Json;
using Serilog.Sinks.PeriodicBatching;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using LogGrid.Client.Middleware;

namespace LogGrid.Client
{
    public static class LogGridExtensions
    {
        public static ILoggingBuilder AddLogGridClient(this ILoggingBuilder builder, IConfiguration configuration)
        {
            var logGridConfig = configuration
                .GetSection("LogGridClient")
                .Get<LogGridClientConfig>() ?? new LogGridClientConfig();

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .ReadFrom.Configuration(configuration);

            // --- ENRICHMENT (IP & Environment) ---
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            string ipAddress = GetLocalIpAddress();
            string projectName = logGridConfig.ApplicationName ?? "UnknownProject";

            loggerConfiguration
                .Enrich.WithProperty("Environment", environment)
                .Enrich.WithProperty("IpAddress", ipAddress)
                .Enrich.WithProperty("ProjectName", projectName);

            var effectiveLevels = LogLevelEvaluator.Evaluate(logGridConfig.DirectClientLogLevels);
            loggerConfiguration = loggerConfiguration.Filter.ByExcluding(logEvent =>
                (logEvent.Level == LogEventLevel.Debug && !effectiveLevels.Debug) ||
                (logEvent.Level == LogEventLevel.Information && !effectiveLevels.Information) ||
                (logEvent.Level == LogEventLevel.Warning && !effectiveLevels.Warning) ||
                (logEvent.Level == LogEventLevel.Error && !effectiveLevels.Error));

            // Register LogArchivalService
            builder.Services.AddHostedService<LogArchivalService>();

            // Apply minimum level overrides
            foreach (var overrideConfig in logGridConfig.MinimumLevelOverrides)
            {
                if (Enum.TryParse<LogEventLevel>(overrideConfig.Value, true, out var level))
                    loggerConfiguration.MinimumLevel.Override(overrideConfig.Key, level);
            }

            if (logGridConfig.Enabled)
            {
                var standardFormatter = new StandardLogTextFormatter();

                // ------------------- CONSOLE LOGS -------------------
                if (logGridConfig.Providers.UseConsole)
                    loggerConfiguration.WriteTo.Console(standardFormatter);

                // ------------------- FILE LOGS ----------------------
                if (logGridConfig.Providers.UseFile)
                {
                    // Calculate file size limit in bytes
                    long? fileSizeLimitBytes = logGridConfig.File.MaxLogFileSizeInMB > 0 
                        ? logGridConfig.File.MaxLogFileSizeInMB * 1024 * 1024 
                        : null;

                    if (string.Equals(logGridConfig.File.OutputStructure, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        loggerConfiguration.WriteTo.File(
                            formatter: standardFormatter,
                            path: logGridConfig.File.Path,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: null, // Disable Serilog retention, handled by ArchivalService
                            fileSizeLimitBytes: fileSizeLimitBytes,
                            rollOnFileSizeLimit: true,
                            shared: true
                        );
                    }
                    else
                    {
                        loggerConfiguration.WriteTo.File(
                            path: logGridConfig.File.Path,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: null, // Disable Serilog retention, handled by ArchivalService
                            fileSizeLimitBytes: fileSizeLimitBytes,
                            rollOnFileSizeLimit: true,
                            shared: true,
                            outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
                        );
                    }
                }



                // ------------------- LOGGRID API SINK -------------------
                if (!string.IsNullOrEmpty(logGridConfig.ApiUrl))
                {
                    var sink = new LogGridSink(new HttpClient(), logGridConfig.ApiUrl);

                    var batchingOptions = new PeriodicBatchingSinkOptions
                    {
                        BatchSizeLimit = 50,
                        Period = TimeSpan.FromSeconds(5),
                        EagerlyEmitFirstEvent = true
                    };

                    loggerConfiguration.WriteTo.Sink(
                        new PeriodicBatchingSink(sink, batchingOptions),
                        LogEventLevel.Debug
                    );
                }
            }

            // Register Serilog with .NET logging system
            var logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;
            builder.AddSerilog(logger, dispose: true);

            // Register LogGridDirectClient and its configuration automatically
            builder.Services.Configure<LogGridClientConfig>(configuration.GetSection("LogGridClient"));
            builder.Services.AddScoped<LogGridDirectClient>();

            return builder;
        }

        public static IApplicationBuilder UseLogGridMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<LogContextMiddleware>();
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
