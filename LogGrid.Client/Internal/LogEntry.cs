using System;
using System.Collections.Generic;

namespace LogGrid.Client.Internal
{
    internal class LogEntry
    {
        public string Application { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, object>? Properties { get; set; }
    }
}
