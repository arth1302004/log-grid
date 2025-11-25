namespace LogGrid.Client
{
    public class LogGridClientConfig
    {
        public bool Enabled { get; set; } = false;
        public string ApiUrl { get; set; } = "http://localhost:5190";
        public ProviderConfig Providers { get; set; } = new ProviderConfig();
        public FileConfig File { get; set; } = new FileConfig();
        public int BatchSize { get; set; } = 50;
        public int BatchPeriodSeconds { get; set; } = 5;
        public string MinimumLogLevel { get; set; } = "Information"; // Default to Information
        public List<string> Enrichers { get; set; } = new List<string>();
        public Dictionary<string, string> MinimumLevelOverrides { get; set; } = new Dictionary<string, string>();
        public string LogStoreMode { get; set; } = "Synchronous"; // Default to Synchronous
        public string ApplicationName { get; set; } = "Unknown";
        public DirectLogLevels DirectClientLogLevels { get; set; } = new DirectLogLevels();
    }

    public class DirectLogLevels
    {
        public bool Info { get; set; } = true;
        public bool Debug { get; set; } = true;
        public bool Warning { get; set; } = true;
        public bool Error { get; set; } = true;
    }

    public class ProviderConfig
    {
        public bool UseFile { get; set; } = false;
        public bool UseConsole { get; set; } = false;
    }



    public class FileConfig
    {
        public int RetentionDays { get; set; } = 7;
        public int ArchiveRetentionDays { get; set; } = 30; // Default 30 days in archive
        public long MaxLogFileSizeInMB { get; set; } = 10; // Default 10MB
        public string Path { get; set; } = "logs"; // Default log file path
        public string OutputStructure { get; set; } = "Json"; // Default to JSON
        public string BufferingCriteria { get; set; } = "FileSize"; // Default to FileSize
    }
}
