using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace LoadRunner.Services;

public interface IFailedRequestLogger
{
    Task LogFailedRequestAsync(TestExecutionResult result, HttpRequestMessage originalRequest);
    void SetReportDirectory(string reportDirectory);
    string GetLogFilePathForEndpoint(string endpoint);
    bool HasFailedRequestsForEndpoint(string endpoint);
    List<string> GetEndpointsWithFailures();
}

public class FailedRequestLogger : IFailedRequestLogger
{
    private readonly ILogger<FailedRequestLogger> _logger;
    private readonly object _lockObject = new object();
    private string _reportDirectory = string.Empty;
    private readonly HashSet<string> _endpointsWithFailures = new HashSet<string>();

    public FailedRequestLogger(ILogger<FailedRequestLogger> logger)
    {
        _logger = logger;
    }

    public void SetReportDirectory(string reportDirectory)
    {
        _reportDirectory = reportDirectory;
        Directory.CreateDirectory(_reportDirectory);
    }

    public async Task LogFailedRequestAsync(TestExecutionResult result, HttpRequestMessage originalRequest)
    {
        if (result.IsSuccess || string.IsNullOrWhiteSpace(_reportDirectory))
            return;

        try
        {
            var endpoint = result.RequestName ?? ExtractEndpointFromUrl(result.Url ?? "unknown");
            var logFilePath = GetLogFilePathForEndpoint(endpoint);
            
            // Track this endpoint has failures
            lock (_lockObject)
            {
                _endpointsWithFailures.Add(endpoint);
            }

            var logEntry = await CreateLogEntryAsync(result, originalRequest);
            
            // Append to log file (thread-safe)
            lock (_lockObject)
            {
                File.AppendAllText(logFilePath, logEntry);
            }

            _logger.LogDebug("Failed request logged to: {LogFilePath}", logFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log failed request for {Url}", result.Url);
        }
    }

    public string GetLogFilePathForEndpoint(string endpoint)
    {
        var safeEndpointName = SanitizeFileName(endpoint);
        return Path.Combine(_reportDirectory, $"failed-requests-{safeEndpointName}.log");
    }

    public bool HasFailedRequestsForEndpoint(string endpoint)
    {
        lock (_lockObject)
        {
            return _endpointsWithFailures.Contains(endpoint);
        }
    }

    public List<string> GetEndpointsWithFailures()
    {
        lock (_lockObject)
        {
            return _endpointsWithFailures.ToList();
        }
    }

    private async Task<string> CreateLogEntryAsync(TestExecutionResult result, HttpRequestMessage originalRequest)
    {
        var logEntry = new StringBuilder();
        
        logEntry.AppendLine("=" + new string('=', 80));
        logEntry.AppendLine($"FAILED REQUEST LOG - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        logEntry.AppendLine("=" + new string('=', 80));
        
        // Request Information
        logEntry.AppendLine();
        logEntry.AppendLine("REQUEST DETAILS:");
        logEntry.AppendLine($"  Method: {result.Method}");
        logEntry.AppendLine($"  URL: {result.Url}");
        logEntry.AppendLine($"  Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
        
        // Request Headers
        logEntry.AppendLine();
        logEntry.AppendLine("REQUEST HEADERS:");
        foreach (var header in result.RequestHeaders)
        {
            logEntry.AppendLine($"  {header.Key}: {header.Value}");
        }

        // Request Body (if available)
        if (originalRequest.Content != null)
        {
            try
            {
                var requestBody = await originalRequest.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    logEntry.AppendLine();
                    logEntry.AppendLine("REQUEST BODY:");
                    logEntry.AppendLine(FormatJsonBody(requestBody));
                }
            }
            catch (Exception ex)
            {
                logEntry.AppendLine();
                logEntry.AppendLine("REQUEST BODY: [Error reading body: " + ex.Message + "]");
            }
        }
        
        // Response Information
        logEntry.AppendLine();
        logEntry.AppendLine("RESPONSE DETAILS:");
        logEntry.AppendLine($"  Status Code: {result.StatusCode}");
        logEntry.AppendLine($"  Response Time: {result.ResponseTime.TotalMilliseconds:F2} ms");
        logEntry.AppendLine($"  Response Size: {result.ResponseSizeBytes} bytes");
        
        // Response Headers
        if (result.ResponseHeaders.Any())
        {
            logEntry.AppendLine();
            logEntry.AppendLine("RESPONSE HEADERS:");
            foreach (var header in result.ResponseHeaders)
            {
                logEntry.AppendLine($"  {header.Key}: {header.Value}");
            }
        }
        
        // Response Body
        if (!string.IsNullOrWhiteSpace(result.ResponseBody))
        {
            logEntry.AppendLine();
            logEntry.AppendLine("RESPONSE BODY:");
            logEntry.AppendLine(FormatJsonBody(result.ResponseBody));
        }
        
        // Error Information
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            logEntry.AppendLine();
            logEntry.AppendLine("ERROR MESSAGE:");
            logEntry.AppendLine($"  {result.ErrorMessage}");
        }
        
        // Validation Failures
        if (result.ValidationResult != null && !result.ValidationResult.IsSuccess)
        {
            logEntry.AppendLine();
            logEntry.AppendLine("VALIDATION FAILURES:");
            foreach (var reason in result.ValidationResult.FailureReasons)
            {
                logEntry.AppendLine($"  - {reason}");
            }
        }
        
        logEntry.AppendLine();
        logEntry.AppendLine();
        
        return logEntry.ToString();
    }

    private string ExtractEndpointFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var pathSegments = uri.PathAndQuery.Trim('/').Split('/', '?');
            
            // Take the first few meaningful segments for the endpoint name
            var endpointParts = pathSegments
                .Take(3) // Take up to 3 path segments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();
                
            return endpointParts.Any() ? string.Join("-", endpointParts) : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        
        // Replace common problematic characters
        sanitized = sanitized.Replace(" ", "-")
                          .Replace(":", "-")
                          .Replace("/", "-")
                          .Replace("\\", "-")
                          .Replace("?", "-")
                          .Replace("&", "-");
                          
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private string FormatJsonBody(string body)
    {
        try
        {
            // Try to format as JSON for better readability
            var jsonDoc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch
        {
            // If not JSON, return as-is with some indentation
            return "  " + body.Replace("\n", "\n  ");
        }
    }
}
