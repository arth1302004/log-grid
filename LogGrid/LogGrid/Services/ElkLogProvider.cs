using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LogGrid.Models;
using Microsoft.Extensions.Options;

namespace LogGrid.Services
{
    public class ElkLogProvider : ILogProvider, IDisposable
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<LoggingProviders> _loggingProviders;
        private readonly ILogger<ElkLogProvider> _logger;
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly Timer _timer;

        public ElkLogProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<LoggingProviders> loggingProviders, ILogger<ElkLogProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _loggingProviders = loggingProviders;
            _logger = logger;

            var elkSettings = _loggingProviders.CurrentValue.ELK;
            _timer = new Timer(ProcessQueue, null, TimeSpan.Zero, TimeSpan.FromSeconds(elkSettings.BatchPeriodSeconds));
        }

        public Task LogAsync(LogEntry logEntry)
        {
            if (!_loggingProviders.CurrentValue.UseELK)
            {
                return Task.CompletedTask;
            }

            _logQueue.Enqueue(logEntry);
            return Task.CompletedTask;
        }

        private async void ProcessQueue(object? state)
        {
            var elkSettings = _loggingProviders.CurrentValue.ELK;
            if (string.IsNullOrEmpty(elkSettings.Uri) || string.IsNullOrEmpty(elkSettings.Index))
            {
                _logger.LogWarning("ELK settings are not configured. Skipping log forwarding.");
                return;
            }

            var logsToSend = new List<LogEntry>();
            while (_logQueue.TryDequeue(out var logEntry))
            {
                logsToSend.Add(logEntry);
                if (logsToSend.Count >= elkSettings.BatchSize)
                {
                    await SendBatchAsync(logsToSend, elkSettings);
                    logsToSend.Clear();
                }
            }

            if (logsToSend.Count > 0)
            {
                await SendBatchAsync(logsToSend, elkSettings);
            }
        }

        private async Task SendBatchAsync(List<LogEntry> logs, ElkSettings elkSettings)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ELK");
                var endpoint = new Uri(new Uri(elkSettings.Uri), "_bulk");

                var payload = new StringBuilder();
                foreach (var log in logs)
                {
                    payload.AppendLine(JsonSerializer.Serialize(new { index = new { _index = elkSettings.Index } }));
                    payload.AppendLine(JsonSerializer.Serialize(log));
                }

                var response = await client.PostAsync(endpoint, new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to send log batch to ELK. Status: {StatusCode}, Response: {Response}", response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending log batch to ELK. The endpoint might be unavailable.");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
