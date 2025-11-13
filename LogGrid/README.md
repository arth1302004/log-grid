# LogGrid Microservice

LogGrid is a standalone, configurable, and modular logging microservice that centralizes log collection for multiple backend systems. It writes logs to local files and optionally forwards them to an ELK stack (Elasticsearch).

## 1. How to Run the Microservice

### Prerequisites
- .NET 8.0 SDK
- An ELK Stack running (if you want to test ELK integration). Make sure the Elasticsearch port (default 9200) is accessible.

### Configuration
1.  Open `LogGrid/LogGrid/appsettings.json`.
2.  Configure the `LoggingProviders` section:
    - `UseFile`: Set to `true` to write logs to local JSON files in a `logs/` directory.
    - `UseELK`: Set to `true` to forward logs to Elasticsearch.
    - `ELK.Uri`: Change `http://<your-elk-server-ip>:9200` to your actual Elasticsearch endpoint.
    - `ELK.Index`: The name of the Elasticsearch index where logs will be stored.

### Running the Service
Navigate to the project directory and run the service:
```bash
cd /home/administrator/Desktop/InternManagementSystem/InternManagementSystem/LogGrid/LogGrid
dotnet run
```
The API will be available at `https://localhost:7193` or `http://localhost:5193` (check the console output for the exact URL).

## 2. How to Send Logs from Another .NET App

You can use a simple client class in any other .NET service (like `InternAttendanceSystem`) to send logs to LogGrid.

### Example `LoggingClient` Class

```csharp
// Add this class to your other backend service
public static class LoggingClient
{
    // Consider making HttpClient static and reusing it.
    private static readonly HttpClient HttpClient = new();
    // This URL should come from your configuration
    private const string LogGridApiUrl = "http://localhost:5193/api/logs"; 

    public static async Task SendLogAsync(string application, string level, string message, Dictionary<string, object>? properties = null)
    {
        var logEntry = new
        {
            Application = application,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Properties = properties
        };

        try
        {
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(logEntry), System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(LogGridApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                // Handle failure to log (e.g., write to a local fallback file or console)
                Console.WriteLine($"Failed to send log to LogGrid. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions, e.g., LogGrid is down
            Console.WriteLine($"Exception while sending log to LogGrid: {ex.Message}");
        }
    }
}
```

### Usage in your service:

```csharp
// Example: In a controller or service in InternAttendanceSystem
var properties = new Dictionary<string, object> { { "UserId", 42 } };
await LoggingClient.SendLogAsync(
    "InternAttendanceSystem", 
    "Error", 
    "Database connection failed on server 'PROD-DB-01'",
    properties
);
```

## 3. How to Verify Logs

### Local File Verification
If `UseFile` is `true`, a `logs` directory will be created inside `LogGrid/LogGrid/bin/Debug/net8.0`. Inside, you will find JSON files named by date (e.g., `2025-11-10.json`). Each file will contain the logs sent on that day.

### Kibana Verification
If `UseELK` is `true` and your configuration is correct:
1.  Open Kibana in your browser (usually at `http://<your-elk-server-ip>:5601`).
2.  Go to **Management > Stack Management > Index Patterns**.
3.  Click **Create index pattern**.
4.  Enter the index name you configured in `appsettings.json` (e.g., `loggrid-logs*`).
5.  Choose a time field (select `@timestamp` if available, or the `Timestamp` field from your logs).
6.  Click **Create index pattern**.
7.  Go to the **Discover** tab. You should see your logs appearing in near real-time. You can filter them by `Application`, `Level`, or any other field.

## 4. Component Diagram

The data flow is as follows:

```
[Client App (e.g., InternAttendanceSystem)]
         |
         | (HTTP POST to /api/logs)
         v
[LogGrid Microservice]
    |    |
    |    +--> [FileLogProvider] --> [logs/yyyy-MM-dd.json]
    |
    +--> [ElkLogProvider] -->(HTTP POST)--> [Elasticsearch]
                                                 |
                                                 v
                                             [Kibana] (for visualization)
```
