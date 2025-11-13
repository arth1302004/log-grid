using LogGrid.Models;
using LogGrid.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Settings
builder.Services.Configure<LoggingProviders>(builder.Configuration.GetSection("LoggingProviders"));

// 2. Add services to the container.
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<ILogProvider, FileLogProvider>();
builder.Services.AddSingleton<ILogProvider, ElkLogProvider>();

// Add HttpClientFactory for ELK provider
builder.Services.AddHttpClient("ELK");

// Add the background service for log retention
builder.Services.AddHostedService<LogRetentionService>();

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

app.Run();