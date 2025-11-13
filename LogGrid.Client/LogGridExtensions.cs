using LogGrid.Client.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace LogGrid.Client
{
    public static class LogGridExtensions
    {
        public static ILoggingBuilder AddLogGrid(this ILoggingBuilder builder, IConfiguration configuration)
        {
            builder.Services.Configure<LogGridClientConfig>(configuration);
            builder.Services.AddHttpClient("LogGrid");
            builder.Services.AddSingleton<LogGridClientProcessor>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<LogGridClientProcessor>());

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, LogGridClientProvider>(sp =>
                {
                    var config = sp.GetRequiredService<IOptionsMonitor<LogGridClientConfig>>();
                    var processor = sp.GetRequiredService<LogGridClientProcessor>();
                    var webHostEnvironment = sp.GetRequiredService<IWebHostEnvironment>();
                    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

                    return new LogGridClientProvider(config, processor, webHostEnvironment, httpContextAccessor);
                })
            );

            return builder;
        }
    }
}