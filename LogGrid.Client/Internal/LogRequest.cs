using System;
using System.Collections.Generic;

namespace LogGrid.Client.Internal
{
    public class LogRequest
    {
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object>? Properties { get; set; }
        public Exception? Exception { get; set; }
    }
}
