# LogGrid.Client Configuration Guide

**Version:** 1.0  
**Date:** November 24, 2025  
**Target Audience:** Backend Developers & DevOps Engineers

---

## 1. Introduction

**LogGrid.Client** is a standardized logging library designed to unify logging practices across the organization. It wraps the Serilog engine to provide:
*   **Consistent JSON Structure**: Ensures all logs follow a strict schema for easy parsing.
*   **Automatic Context**: Automatically captures `UserAgent`, `UserId`, `TraceId`, and `IPAddress`.
*   **Smart Archival**: Automatically manages log file lifecycle (rotation and deletion).
*   **Dual-Mode Logging**: Supports standard `ILogger` for general info and `LogGridDirectClient` for rich debugging with DTOs.

---

## 2. Integration Steps

### Step 1: Add Project Reference
Ensure your target project references the `LogGrid.Client` library.

### Step 2: Configure `Program.cs`
You must register the service and the middleware.

**A. Service Registration**  
Place this line **before** `builder.Build()`:
```csharp
// Registers Serilog, reads config, and sets up sinks
// AUTOMATICALLY registers LogGridDirectClient for you
builder.Logging.AddLogGridClient(builder.Configuration);
```

**B. Middleware Registration**  
Place this line **immediately after** `app.Build()` and before any other middleware:
```csharp
var app = builder.Build();

// CRITICAL: Must be first to capture context for all requests
app.UseLogGridMiddleware(); 

// ... other middleware (Cors, Auth, etc.)
```

---

## 3. Configuration (`appsettings.json`)

Copy the following section into your `appsettings.json`.

```json
"LogGridClient": {
  "Enabled": true,
  "ApplicationName": "YourServiceName",
  "DirectClientLogLevels": {
    "Info": true,
    "Debug": true,
    "Warning": true,
    "Error": true
  },
  "MinimumLevelOverrides": {
    "Microsoft": "Fatal",
    "System": "Fatal"
  },
  "Providers": {
    "UseFile": true,
    "UseConsole": true,
    "UseELK": false
  },
  "File": {
    "RetentionDays": 7,
    "ArchiveRetentionDays": 30,
    "MaxLogFileSizeInMB": 10,
    "Path": "../Logs/log-.json",
    "OutputStructure": "json"
  }
}
```

### Configuration Reference

| Key | Description | Recommended Value |
| :--- | :--- | :--- |
| `ApplicationName` | Identifies the source service in centralized logs. | Service Name (e.g., "PaymentService") |
| `DirectClientLogLevels` | Master switches to toggle specific log levels on/off globally. | All `true` for Dev; `Debug=false` for Prod |
| `MinimumLevelOverrides` | Silences noisy logs from external libraries. | Keep `Microsoft` at `Fatal` to reduce noise |
| `Providers` | Destinations for your logs. | `UseFile: true`, `UseConsole: true` |
| `File.RetentionDays` | Days to keep active logs before archiving. | `7` |
| `File.ArchiveRetentionDays` | Days to keep archived logs before permanent deletion. | `30` |

---

## 4. Usage Guidelines

### Standard Logging (General Info)
Use the standard Microsoft `ILogger` interface for general application flow.

```csharp
public class PaymentService
{
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ILogger<PaymentService> logger)
    {
        _logger = logger;
    }

    public void ProcessPayment()
    {
        _logger.LogInformation("Processing payment started.");
    }
}
```

### Advanced Debugging (With DTOs)
Use `LogGridDirectClient` when you need to inspect complex objects.

```csharp
public class PaymentService
{
    private readonly LogGridDirectClient _clientLogger;

    public PaymentService(LogGridDirectClient clientLogger)
    {
        _clientLogger = clientLogger;
    }

    public void ProcessPayment(PaymentDto dto)
    {
        // This will serialize the DTO into the log ONLY if Debug is enabled
        _clientLogger.Debug("Payment payload received", dto, new Dictionary<string, object>());
    }
}
```

---

## 5. Security & Best Practices

### 1. Masking Sensitive Data
**Never** log raw passwords, tokens, or PII. Use the `[JsonIgnore]` attribute on your DTOs to automatically exclude them from `LogGridDirectClient` serialization.

```csharp
public class LoginDto
{
    public string Username { get; set; }

    [JsonIgnore] // <--- Prevents this from appearing in logs
    public string Password { get; set; }
}
```

### 2. Performance
*   **Production**: Set `"Debug": false` in `DirectClientLogLevels` to avoid performance overhead from DTO serialization.
*   **Development**: Enable all levels to troubleshoot issues.

### 3. Archival Policy
*   Logs are automatically moved to `../Logs/Archive/` after **7 days** or when they exceed **10MB**.
*   Archived logs are permanently deleted after **30 days**.
