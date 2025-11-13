using LogGrid.Models;
using LogGrid.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogGrid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private readonly LoggingService _loggingService;
        private readonly ILogger<LogsController> _logger;
        private readonly IOptionsMonitor<LoggingProviders> _loggingProviders;

        public LogsController(LoggingService loggingService, ILogger<LogsController> logger, IOptionsMonitor<LoggingProviders> loggingProviders)
        {
            _loggingService = loggingService;
            _logger = logger;
            _loggingProviders = loggingProviders;
        }

        [HttpPost]
        public async Task<IActionResult> PostLog([FromBody] LogEntry logEntry)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Basic validation
            if (string.IsNullOrEmpty(logEntry.Application) || string.IsNullOrEmpty(logEntry.Level) || string.IsNullOrEmpty(logEntry.Message))
            {
                return BadRequest("Log entry must contain Application, Level, and Message.");
            }

            if (logEntry.Timestamp == default)
            {
                logEntry.Timestamp = DateTime.UtcNow;
            }

            if (!_loggingProviders.CurrentValue.IncludeTraceId && logEntry.Properties != null && logEntry.Properties.ContainsKey("TraceId"))
            {
                logEntry.Properties.Remove("TraceId");
            }

            try
            {
                await _loggingService.LogAsync(logEntry);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing a log entry.");
                return StatusCode(500, "An internal server error occurred.");
            }
        }
    }
}
