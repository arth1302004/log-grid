using LogGrid.Client;
using LogGrid.Models;
using LogGrid.Services;
using Serilog;
using Serilog.Events;

// Configure Serilog early for bootstrap logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .LogGrid(context.Configuration) // Use the custom LogGrid Serilog extension
    );

    // 1. Configure Settings (Keep for other configurations if any, but LoggingProviders will be replaced by Serilog config)
    // builder.Services.Configure<LoggingProviders>(builder.Configuration.GetSection("LoggingProviders")); // This section will be handled by Serilog

    // 2. Add services to the container.
    // The following services are now handled by Serilog sinks or are no longer needed with Serilog
    // builder.Services.AddSingleton<LoggingService>();
    // builder.Services.AddSingleton<ILogProvider, FileLogProvider>();
    // builder.Services.AddSingleton<ILogProvider, ElkLogProvider>();

    // Add HttpClientFactory for ELK provider (if still needed for other purposes, otherwise remove)
    // builder.Services.AddHttpClient("ELK");

    // Add the background service for log retention (This service is still relevant for file cleanup, but its logging part might change)
    builder.Services.AddHostedService<LogRetentionService>();

    builder.Services.AddHttpContextAccessor();
    // builder.Services.AddLogging(loggingBuilder =>
    // {
    //     loggingBuilder.AddLogGridClient(builder.Configuration.GetSection("LogGridClient"));
    // }); // Replaced by Serilog configuration

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    // The old ILogger instance is no longer needed here if Serilog is fully integrated
    // var logger = app.Services.GetRequiredService<ILogger<Program>>();
    // logger.LogInformation("The application is starting up. This is a test log message.");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}