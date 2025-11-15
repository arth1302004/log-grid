using System.Text.Json.Serialization;

namespace LogGrid.Models;

public class LogEntry
{
    [JsonPropertyName("Application")]
    public string Application { get; set; } = string.Empty;
        
    [JsonPropertyName("Level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("Source")]
    public string Source { get; set; } = string.Empty;
    
    [JsonPropertyName("OutputFormat")]
    public string OutputFormat { get; set; } = "json";

    [JsonPropertyName("Properties")]
    public Dictionary<string, object>? Properties { get; set; }
}
