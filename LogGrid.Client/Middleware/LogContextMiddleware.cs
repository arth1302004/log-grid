using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LogGrid.Client.Middleware
{
    public class LogContextMiddleware
    {
        private readonly RequestDelegate _next;

        public LogContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            using (LogContext.PushProperty("UserAgent", userAgent))
            using (LogContext.PushProperty("UserId", userId))
            {
                await _next(context);
            }
        }
    }
}
