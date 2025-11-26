using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;

namespace LogGrid.Client.Formatting;

internal sealed class StandardLogTextFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var payload = StandardLogSerializer.BuildPayload(logEvent);
        var json = JsonSerializer.Serialize(payload, StandardLogSerializer.JsonOptions);
        output.WriteLine(json);
    }
}

internal static class StandardLogSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null, // Use exact property names from the record
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static StandardLogPayload BuildPayload(LogEvent logEvent)
    {
        var traceId = ExtractString(logEvent, "TraceId") ?? Activity.Current?.TraceId.ToString();
        var requestId = ExtractString(logEvent, "RequestId") ?? Activity.Current?.SpanId.ToString();
        var correlationId = ExtractString(logEvent, "CorrelationId") ?? Activity.Current?.TraceId.ToString();
        var ipAddress = ExtractString(logEvent, "IpAddress");
        var environment = ExtractString(logEvent, "Environment");
        var projectName = ExtractString(logEvent, "ProjectName");
        var userAgent = ExtractString(logEvent, "UserAgent");
        var userId = ExtractString(logEvent, "UserId");
        var source = ExtractString(logEvent, "SourceContext") ?? "LogGrid.Client";

        var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Message"] = logEvent.RenderMessage()
        };

        // For Debug/Verbose, we include all properties (DTOs, etc.)
        // For Info/Warn/Error/Fatal, we ONLY include "Message"
        if (logEvent.Level == LogEventLevel.Debug || logEvent.Level == LogEventLevel.Verbose)
        {
            foreach (var property in logEvent.Properties)
            {
                if (IsReservedKey(property.Key)) continue;
                properties[property.Key] = Simplify(property.Value);
            }
        }

        return new StandardLogPayload
        {
            Timestamp = logEvent.Timestamp.ToString("o"),
            Level = MapLevel(logEvent.Level),
            TraceId = traceId,
            RequestId = requestId,
            CorrelationId = correlationId,
            Source = source,
            ProjectName = projectName ?? "Unknown",
            Environment = environment,
            IPAddress = ipAddress,
            UserAgent = userAgent,
            UserId = userId,
            Properties = properties,
            ErrorStack = logEvent.Exception?.ToString()
        };
    }

    private static string MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Debug => "debug",
        LogEventLevel.Information => "information",
        LogEventLevel.Warning => "warning",
        LogEventLevel.Error => "error",
        LogEventLevel.Fatal => "error",
        LogEventLevel.Verbose => "debug",
        _ => level.ToString().ToLowerInvariant()
    };

    private static string? ExtractString(LogEvent logEvent, string propertyName)
    {
        if (logEvent.Properties.TryGetValue(propertyName, out var value))
        {
            return Simplify(value)?.ToString();
        }

        return null;
    }

    private static bool IsReservedKey(string key) =>
        key is "TraceId" or "RequestId" or "CorrelationId" or "SourceContext" or "IpAddress" or "Environment" or "ProjectName" or "UserAgent" or "UserId";

    private static object? Simplify(LogEventPropertyValue value) => value switch
    {
        ScalarValue scalar => scalar.Value,
        SequenceValue seq => seq.Elements.Select(Simplify).ToArray(),
        DictionaryValue dict => dict.Elements.ToDictionary(
            kvp => kvp.Key.Value ?? string.Empty,
            kvp => Simplify(kvp.Value)),
        StructureValue structure => structure.Properties.ToDictionary(
            prop => prop.Name ?? string.Empty,
            prop => Simplify(prop.Value)),
        _ => value.ToString()
    };
}



internal sealed record StandardLogPayload
{
    [System.Text.Json.Serialization.JsonPropertyName("Timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("Level")]
    public string Level { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("TraceId")]
    public string? TraceId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("RequestId")]
    public string? RequestId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("CorrelationId")]
    public string? CorrelationId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("Source")]
    public string Source { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("ProjectName")]
    public string ProjectName { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("Environment")]
    public string? Environment { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("IPAddress")]
    public string? IPAddress { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("UserAgent")]
    public string? UserAgent { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("UserId")]
    public string? UserId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("Properties")]
    public IDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();

    [System.Text.Json.Serialization.JsonPropertyName("ErrorStack")]
    public string? ErrorStack { get; init; }
}

