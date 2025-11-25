using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LogGrid.Client.Formatting;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace LogGrid.Client
{
    public sealed class LogGridSink : IBatchedLogEventSink
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;

        public LogGridSink(HttpClient httpClient, string apiUrl)
        {
            _httpClient = httpClient;
            _apiUrl = apiUrl;
        }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            if (events == null) return;

            var payload = events.Select(evt =>
            {
                var logPayload = StandardLogSerializer.BuildPayload(evt);
                return logPayload;
            }).ToArray();

            if (payload.Length == 0) return;

            try
            {
                await _httpClient.PostAsJsonAsync(_apiUrl, payload);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[LogGridSink] Failed to send logs to API: {ex.Message}");
            }
        }

        public Task OnEmptyBatchAsync() => Task.CompletedTask;
    }

    // Wrapper to adapt to Serilog
    public sealed class LogGridPeriodicSink : PeriodicBatchingSink
    {
        public LogGridPeriodicSink(LogGridSink sink, int batchSizeLimit, TimeSpan period)
            : base(sink, new PeriodicBatchingSinkOptions
            {
                BatchSizeLimit = batchSizeLimit,
                Period = period,
                EagerlyEmitFirstEvent = true
            })
        {
        }
    }
}
