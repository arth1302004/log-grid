namespace LogGrid.Client
{
    public class LogGridClientConfig
    {
        public bool Enabled { get; set; } = false;
        public string ApiUrl { get; set; } = "http://localhost:5190";
        public ProviderConfig Providers { get; set; } = new ProviderConfig();
        public ELKConfig ELK { get; set; } = new ELKConfig();
        public FileConfig File { get; set; } = new FileConfig();
    }

    public class ProviderConfig
    {
        public bool UseFile { get; set; } = false;
        public bool UseELK { get; set; } = false;
        public bool UseConsole { get; set; } = false;
    }

    public class ELKConfig
    {
        public string Uri { get; set; } = "http://192.168.5.113:9200";
        public string Index { get; set; } = "intern-dev-logs";
    }

    public class FileConfig
    {
        public int RetentionDays { get; set; } = 7;
    }
}
