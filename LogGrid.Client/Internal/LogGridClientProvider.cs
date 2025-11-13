using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace LogGrid.Client.Internal
{
    [ProviderAlias("LogGrid")]
    internal class LogGridClientProvider : ILoggerProvider
    {
        private readonly IOptionsMonitor<LogGridClientConfig> _config;
        private readonly LogGridClientProcessor _processor;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ConcurrentDictionary<string, LogGridClientLogger> _loggers = new ConcurrentDictionary<string, LogGridClientLogger>();

        public LogGridClientProvider(
            IOptionsMonitor<LogGridClientConfig> config, 
            LogGridClientProcessor processor,
            IWebHostEnvironment webHostEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _processor = processor;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new LogGridClientLogger(name, _processor, _webHostEnvironment, _httpContextAccessor));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
