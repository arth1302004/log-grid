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
        private readonly LogGridClientProcessor _processor;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LogGridClientLogger(
            string name, 
            LogGridClientProcessor processor,
            IWebHostEnvironment webHostEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _name = name;
            _processor = processor;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var properties = new Dictionary<string, object>
            {
                { "Category", _name },
                { "Environment", _webHostEnvironment.EnvironmentName }
            };

            if (exception != null)
            {
                properties.Add("Exception", exception.ToString());
            }

            if (Activity.Current?.Id != null)
            {
                properties.Add("TraceId", Activity.Current.Id);
            }

            if (_httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress != null)
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
                Properties = properties
            };

            _processor.EnqueueLog(logEntry);
        }
    }
}
