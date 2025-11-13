using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LogGrid.Client.Internal
{
    internal class LogGridClientProcessor : BackgroundService
    {
        private readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LogGridClientConfig _config;

        public LogGridClientProcessor(IHttpClientFactory httpClientFactory, LogGridClientConfig config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public void EnqueueLog(LogEntry logEntry)
        {
            _queue.Enqueue(logEntry);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_queue.IsEmpty && _config.Enabled)
                {
                    var client = _httpClientFactory.CreateClient("LogGrid");
                    if (_queue.TryDequeue(out var logEntry))
                    {
                        try
                        {
                            var response = await client.PostAsJsonAsync(_config.ApiUrl + "/api/logs", logEntry, stoppingToken);
                            if (!response.IsSuccessStatusCode)
                            {
                                // Basic retry: re-queue the log entry
                                EnqueueLog(logEntry);
                                await Task.Delay(1000, stoppingToken); // Wait a bit before retrying
                            }
                        }
                        catch
                        {
                            // If sending fails, re-queue and wait.
                            EnqueueLog(logEntry);
                            await Task.Delay(5000, stoppingToken); // Wait longer if the service is unavailable
                        }
                    }
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }
    }
}
