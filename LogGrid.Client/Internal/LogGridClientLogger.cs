using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace LogGrid.Client.Internal
{
    internal class LogGridClientLogger : ILogger
    {
        private readonly string _name;
        private readonly LogGridClientConfig _config;
        private readonly LogGridClientProcessor _processor;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LogGridClientLogger(
            string name,
            LogGridClientConfig config,
            LogGridClientProcessor processor,
            IWebHostEnvironment webHostEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _name = name;
            _config = config;
            _processor = processor;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None)
            {
                return false;
            }

            if (!Enum.TryParse(_config.MinimumLogLevel, true, out LogLevel minimumLogLevel))
            {
                minimumLogLevel = LogLevel.Information; // Default if parsing fails
            }

            return logLevel >= minimumLogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var properties = new Dictionary<string, object>();

            if (_config.Enrichers.Contains("Category"))
            {
                properties.Add("Category", _name);
            }
            if (_config.Enrichers.Contains("Environment"))
            {
                properties.Add("Environment", _webHostEnvironment.EnvironmentName);
            }

            if (exception != null)
            {
                properties.Add("Exception", exception.ToString());
            }

            if (_config.Enrichers.Contains("TraceId") && Activity.Current?.Id != null)
            {
                properties.Add("TraceId", Activity.Current.Id);
            }

            if (_config.Enrichers.Contains("IpAddress") && _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress != null)
            {
                properties.Add("IpAddress", _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString());
            }

            var logEntry = new LogEntry
            {
                Application = Assembly.GetEntryAssembly()?.GetName().Name ?? "UnknownApp",
                Timestamp = DateTime.UtcNow,
                Level = logLevel.ToString(),
                Message = message,
                Source = _name,
                OutputFormat = _config.File.OutputStructure,
                Properties = properties
            };

            _processor.EnqueueLog(logEntry);
        }
    }
}
