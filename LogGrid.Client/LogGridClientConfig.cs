namespace LogGrid.Client
{
    public class LogGridClientConfig
    {
        public bool Enabled { get; set; } = false;
        public string ApiUrl { get; set; } = "http://localhost:5190";
        public ProviderConfig Providers { get; set; } = new ProviderConfig();
        public ELKConfig ELK { get; set; } = new ELKConfig();
        public FileConfig File { get; set; } = new FileConfig();
        public string MinimumLogLevel { get; set; } = "Information"; // Default to Information
        public List<string> Enrichers { get; set; } = new List<string>();
        public string LogStoreMode { get; set; } = "Synchronous"; // Default to Synchronous
    }

    public class ProviderConfig
    {
        public bool UseFile { get; set; } = false;
        public bool UseELK { get; set; } = false;
        public bool UseConsole { get; set; } = false;
    }

    public class ELKConfig
    {
        public string ApiUrl { get; set; } = "http://192.168.5.113:9200"; // Renamed from Uri
        public string Index { get; set; } = "intern-dev-logs";
    }

    public class FileConfig
    {
        public int RetentionDays { get; set; } = 7;
        public string Path { get; set; } = "logs"; // Default log file path
        public string OutputStructure { get; set; } = "Json"; // Default to JSON
        public string BufferingCriteria { get; set; } = "FileSize"; // Default to FileSize
    }
}
