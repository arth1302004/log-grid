namespace LogGrid.Models
{
    public class LoggingProviders
    {
        public bool UseFile { get; set; }
        public bool UseELK { get; set; }
        public bool IncludeTraceId { get; set; } = true;
        public ElkSettings ELK { get; set; } = new();
        public FileSettings File { get; set; } = new();
    }

    public class ElkSettings
    {
        public string Uri { get; set; } = string.Empty;
        public string Index { get; set; } = string.Empty;
        public int BatchSize { get; set; } = 50;
        public int BatchPeriodSeconds { get; set; } = 5;
    }

    public class FileSettings
    {
        public int RetentionDays { get; set; } = 7;
        public string Path { get; set; } = "logs";
        public string OutputFormat { get; set; } = "json";
    }
}
