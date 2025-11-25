using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace LogGrid.Client
{
    public class LogGridDirectClient : IDisposable
    {

        private readonly Serilog.ILogger _hostLogger;
        private readonly IDisposable? _changeSubscription;
        private LogGridClientConfig _config;
        private EffectiveLogLevels _effectiveLevels;
        private static readonly JsonSerializerOptions DtoSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        public LogGridDirectClient(IOptionsMonitor<LogGridClientConfig> configMonitor, IHttpClientFactory? httpClientFactory = null)
        {
            if (configMonitor == null) throw new ArgumentNullException(nameof(configMonitor));

            _config = CloneConfig(configMonitor.CurrentValue);
            _effectiveLevels = LogLevelEvaluator.Evaluate(_config.DirectClientLogLevels);
            _changeSubscription = configMonitor.OnChange(updated =>
            {
                _config = CloneConfig(updated);
                _effectiveLevels = LogLevelEvaluator.Evaluate(_config.DirectClientLogLevels);
            });

            var httpClient = httpClientFactory?.CreateClient("LogGridClient") ?? new HttpClient();

            // Create the raw HTTP sink
            var httpSink = new LogGridSink(httpClient, _config.ApiUrl);

            // Wrap inside batching sink
            var batchedSink = new LogGridPeriodicSink(
                httpSink,
                batchSizeLimit: Math.Max(1, _config.BatchSize),
                period: TimeSpan.FromSeconds(Math.Max(1, _config.BatchPeriodSeconds))
            );

            // _logger removed to prevent duplicate logs. We rely solely on _hostLogger.

            // Reuse the host Serilog pipeline so that direct-client logs also land in standard sinks
            _hostLogger = Log.Logger.ForContext<LogGridDirectClient>();
        }

        public void Info(string message, Dictionary<string, object> properties)
        {
            if (!_effectiveLevels.Information) return;
            WriteLog(LogEventLevel.Information, message, properties);
        }

        public void Info(string message, Dictionary<string, object> properties, string[] sensitiveKeys)
        {
            if (!_effectiveLevels.Information) return;
            var maskedProperties = MaskProperties(properties, sensitiveKeys);
            WriteLog(LogEventLevel.Information, message, maskedProperties);
        }

        public void Debug(string message, object dto, Dictionary<string, object> properties)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (!_effectiveLevels.Debug) return;
            var dtoJson = JsonSerializer.Serialize(dto, DtoSerializerOptions);
            var enriched = MergeProperties(properties, ("Dto", dto));
            var debugMessage = $"{message} | dto={dtoJson}";
            WriteLog(LogEventLevel.Debug, debugMessage, enriched);
        }

        public void Debug(string message, object dto, Dictionary<string, object> properties, string[] sensitiveKeys)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (!_effectiveLevels.Debug) return;
            
            // Serialize DTO to JSON, then deserialize to dictionary to mask properties if needed
            // Or simpler: just mask the properties dictionary and let DTO be as is? 
            // The requirement says "check if there's any key in matching in the log file". 
            // Usually DTOs are logged as structured data. 
            // Let's mask the properties first. 
            // For DTO, if we want to mask inside it, we'd need to traverse it. 
            // Given the prompt "overridding the log level functions... array of string... iterated to check if there's any key in matching in the log file", 
            // I will assume we mask the `properties` dictionary. 
            // If the DTO itself contains sensitive data, it might be complex to mask without reflection or re-serialization.
            // However, the prompt mentions "value of that key will me masked".
            // Let's try to mask inside the DTO if it's serialized to a string in the message, or if it's added as a property.
            // In the existing Debug method: `var enriched = MergeProperties(properties, ("Dto", dto));`
            // So "Dto" is a key in the properties.
            // If we want to mask fields INSIDE the DTO, we need to convert DTO to dictionary, mask it, and then use that.
            
            var dtoJson = JsonSerializer.Serialize(dto, DtoSerializerOptions);
            // We can try to deserialize to JsonElement or Dictionary to mask
            // But for now, let's stick to masking the `properties` dictionary which is the explicit argument.
            // Wait, if the user wants to mask "PublicKey" in a DTO, and we only mask top-level properties, it won't work if DTO is just an object.
            // But the prompt says "mask the public key in the log" for EncryptionService.
            // Let's look at EncryptionService later.
            // For now, I will implement masking for the `properties` dictionary.
            // AND I will also attempt to mask the DTO if it's possible/easy, 
            // but the prompt specifically talks about "key matching in the log file".
            
            // Let's implement robust masking that checks the properties.
            
            var maskedProperties = MaskProperties(properties, sensitiveKeys);
            
            // Special handling for DTO: if we can, we should mask inside it too.
            // But `dto` is `object`. 
            // Let's serialize DTO to JsonNode/Dictionary, mask, and then use that as Dto.
            object finalDto = dto;
            try 
            {
                var json = JsonSerializer.Serialize(dto, DtoSerializerOptions);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    var maskedDtoDict = MaskProperties(dict, sensitiveKeys);
                    finalDto = maskedDtoDict;
                }
            }
            catch
            {
                // If serialization/deserialization fails (e.g. primitive type), just use original
            }

            var finalDtoJson = JsonSerializer.Serialize(finalDto, DtoSerializerOptions);
            var enriched = MergeProperties(maskedProperties, ("Dto", finalDto));
            var debugMessage = $"{message} | dto={finalDtoJson}";
            WriteLog(LogEventLevel.Debug, debugMessage, enriched);
        }

        public void Warning(string message, Dictionary<string, object> properties)
        {
            if (!_effectiveLevels.Information) return;
            WriteLog(LogEventLevel.Warning, message, properties);
        }

        public void Warning(string message, Dictionary<string, object> properties, string[] sensitiveKeys)
        {
            if (!_effectiveLevels.Warning) return;
            var maskedProperties = MaskProperties(properties, sensitiveKeys);
            WriteLog(LogEventLevel.Warning, message, maskedProperties);
        }

        public void Error(string message, Exception? exception, Dictionary<string, object> properties)
        {
            if (!_effectiveLevels.Error) return;
            WriteLog(LogEventLevel.Error, message, properties, exception);
        }

        public void Error(string message, Exception? exception, Dictionary<string, object> properties, string[] sensitiveKeys)
        {
            if (!_effectiveLevels.Error) return;
            var maskedProperties = MaskProperties(properties, sensitiveKeys);
            WriteLog(LogEventLevel.Error, message, maskedProperties, exception);
        }

        public void Dispose()
        {
            _changeSubscription?.Dispose();

        }

        private static LogGridClientConfig CloneConfig(LogGridClientConfig? source)
        {
            source ??= new LogGridClientConfig();

            return new LogGridClientConfig
            {
                Enabled = source.Enabled,
                ApiUrl = source.ApiUrl,
                Providers = new ProviderConfig
                {
                    UseConsole = source.Providers?.UseConsole ?? false,
                    UseFile = source.Providers?.UseFile ?? false
                },
                File = new FileConfig
                {
                    Path = source.File?.Path ?? "logs",
                    RetentionDays = source.File?.RetentionDays ?? 7,
                    OutputStructure = source.File?.OutputStructure ?? "Json",
                    BufferingCriteria = source.File?.BufferingCriteria ?? "FileSize"
                },
                BatchSize = source.BatchSize,
                BatchPeriodSeconds = source.BatchPeriodSeconds,
                MinimumLogLevel = source.MinimumLogLevel,
                Enrichers = source.Enrichers?.ToList() ?? new List<string>(),
                MinimumLevelOverrides = source.MinimumLevelOverrides != null
                    ? new Dictionary<string, string>(source.MinimumLevelOverrides)
                    : new Dictionary<string, string>(),
                LogStoreMode = source.LogStoreMode,
                ApplicationName = source.ApplicationName,
                DirectClientLogLevels = new DirectLogLevels
                {
                    Info = source.DirectClientLogLevels?.Info ?? true,
                    Debug = source.DirectClientLogLevels?.Debug ?? true,
                    Warning = source.DirectClientLogLevels?.Warning ?? true,
                    Error = source.DirectClientLogLevels?.Error ?? true
                }
            };
        }

        private void WriteLog(LogEventLevel level, string message, Dictionary<string, object>? properties = null, Exception? exception = null)
        {
            // WriteToTarget(_logger, level, message, properties, exception); // Removed to prevent duplicates
            WriteToTarget(_hostLogger, level, message, properties, exception);
        }

        private static void WriteToTarget(Serilog.ILogger target, LogEventLevel level, string message,
            Dictionary<string, object>? properties, Exception? exception)
        {
            var logger = target;

            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    logger = logger.ForContext(kvp.Key, kvp.Value, destructureObjects: true);
                }
            }

            if (exception != null)
                logger.Write(level, exception, "{LogMessage}", message);
            else
                logger.Write(level, "{LogMessage}", message);
        }

        private static Dictionary<string, object> MaskProperties(Dictionary<string, object>? properties, string[] sensitiveKeys)
        {
            if (properties == null) return new Dictionary<string, object>();
            if (sensitiveKeys == null || sensitiveKeys.Length == 0) return new Dictionary<string, object>(properties);

            var maskedProperties = new Dictionary<string, object>(properties, StringComparer.OrdinalIgnoreCase);

            foreach (var key in sensitiveKeys)
            {
                // Check if the key exists in the properties (case-insensitive due to dictionary constructor)
                // We need to find the actual key casing to replace it, or just iterate.
                // Since we used StringComparer.OrdinalIgnoreCase, we can just set it.
                // But we need to check if it actually exists to avoid adding new keys? 
                // The dictionary contains the keys.
                
                // We want to mask the VALUE if the KEY matches.
                // Iterate over keys to find matches
                
                var keysToMask = maskedProperties.Keys.Where(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var k in keysToMask)
                {
                    maskedProperties[k] = "******";
                }
            }

            return maskedProperties;
        }

        private static Dictionary<string, object> MergeProperties(
            Dictionary<string, object>? original,
            params (string Key, object Value)[] additional)
        {
            var merged = original != null
                ? new Dictionary<string, object>(original, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in additional)
            {
                merged[key] = value;
            }

            return merged;
        }

    }
}
