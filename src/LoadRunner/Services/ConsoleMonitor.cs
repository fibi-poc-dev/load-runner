using LoadRunner.Models;
using Microsoft.Extensions.Logging;

namespace LoadRunner.Services;

public interface IConsoleMonitor
{
    Task StartMonitoringAsync(CancellationToken cancellationToken);
    void DisplaySummary();
    void DisplayStartMessage();
}

public class ConsoleMonitor : IConsoleMonitor
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<ConsoleMonitor> _logger;
    private int _consoleWidth = 120;
    private int _displayCounter = 0;
    private int _statusStartLine = 0;
    private readonly object _consoleLock = new object();

    public ConsoleMonitor(
        IMetricsCollector metricsCollector,
        IConfigurationManager configurationManager,
        ILogger<ConsoleMonitor> logger)
    {
        _metricsCollector = metricsCollector;
        _configurationManager = configurationManager;
        _logger = logger;
        
        try
        {
            _consoleWidth = Console.WindowWidth;
        }
        catch
        {
            _consoleWidth = 120; // Default width
        }
        
        // Disable cursor blinking for smoother updates
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // Ignore if not supported
        }
    }

    public void DisplayStartMessage()
    {
        var config = _configurationManager.Configuration;
        
        lock (_consoleLock)
        {
            Console.Clear();
            Console.WriteLine(new string('=', _consoleWidth));
            Console.WriteLine("LoadRunner Performance Test - Starting".PadLeft((_consoleWidth + 35) / 2));
            Console.WriteLine(new string('=', _consoleWidth));
            Console.WriteLine();
            
            Console.WriteLine($"Test Configuration:");
            Console.WriteLine($"  Duration: {config.ExecutionSettings.TestDurationMs / 1000.0} seconds");
            Console.WriteLine($"  Ramp-up: {config.ExecutionSettings.RampUpTimeMs / 1000.0} seconds");
            Console.WriteLine($"  Target TPS: {config.PerformanceSettings.TargetTransactionsPerSecond}");
            Console.WriteLine($"  Max Concurrent Users: {config.PerformanceSettings.MaxConcurrentUsers}");
            Console.WriteLine();
            
            Console.WriteLine("Press Ctrl+C to stop the test...");
            Console.WriteLine();
            
            // Remember where the dynamic status display starts
            _statusStartLine = Console.CursorTop;
        }
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        var config = _configurationManager.Configuration;
        var updateInterval = config.OutputSettings.ConsoleUpdateIntervalMs;
        
        _logger.LogInformation("Starting console monitoring with {UpdateInterval}ms update interval", updateInterval);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DisplayCurrentStatus();
                await Task.Delay(updateInterval, cancellationToken);
                _displayCounter++;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Console monitoring stopped");
        }
    }

    public void DisplaySummary()
    {
        var metrics = _metricsCollector.GetCurrentMetrics();
        var config = _configurationManager.Configuration;
        
        lock (_consoleLock)
        {
            // Re-enable cursor for final display
            try
            {
                Console.CursorVisible = true;
            }
            catch
            {
                // Ignore if not supported
            }
            
            Console.WriteLine();
            Console.WriteLine(new string('=', _consoleWidth));
            Console.WriteLine("LOAD TEST SUMMARY".PadLeft((_consoleWidth + 17) / 2));
            Console.WriteLine(new string('=', _consoleWidth));
            Console.WriteLine();
            
            Console.WriteLine($"Test Duration: {metrics.ElapsedTime:hh\\:mm\\:ss}");
            Console.WriteLine($"Total Requests: {metrics.TotalRequests:N0}");
            Console.WriteLine($"Successful Requests: {metrics.SuccessfulRequests:N0} ({(double)metrics.SuccessfulRequests / Math.Max(metrics.TotalRequests, 1) * 100:F1}%)");
            Console.WriteLine($"Failed Requests: {metrics.FailedRequests:N0} ({metrics.ErrorRate:F1}%)");
            Console.WriteLine($"Validation Failures: {metrics.ValidationFailures:N0} ({metrics.ValidationFailureRate:F1}%)");
            Console.WriteLine();
            
            if (metrics.ResponseTimes.Count > 0)
            {
                Console.WriteLine("Response Time Statistics:");
                Console.WriteLine($"  Average: {metrics.AverageResponseTime:F0}ms");
                Console.WriteLine($"  Minimum: {metrics.MinResponseTime:F0}ms");
                Console.WriteLine($"  Maximum: {metrics.MaxResponseTime:F0}ms");
                Console.WriteLine($"  P50: {metrics.GetPercentile(50):F0}ms");
                Console.WriteLine($"  P90: {metrics.GetPercentile(90):F0}ms");
                Console.WriteLine($"  P95: {metrics.GetPercentile(95):F0}ms");
                Console.WriteLine($"  P99: {metrics.GetPercentile(99):F0}ms");
            }
            
            Console.WriteLine();
            Console.WriteLine("Thresholds:");
            var maxRtStatus = metrics.ResponseTimes.Count > 0 && metrics.GetPercentile(95) <= config.Thresholds.MaxResponseTimeMs ? "✓ PASS" : "✗ FAIL";
            var errorRateStatus = metrics.ErrorRate <= config.Thresholds.MaxErrorRatePercent ? "✓ PASS" : "✗ FAIL";
            var minTpsStatus = metrics.CurrentTransactionsPerSecond >= config.Thresholds.MinTransactionsPerSecond ? "✓ PASS" : "✗ FAIL";
            
            Console.WriteLine($"  Max Response Time ({config.Thresholds.MaxResponseTimeMs}ms): {maxRtStatus}");
            Console.WriteLine($"  Max Error Rate ({config.Thresholds.MaxErrorRatePercent:F1}%): {errorRateStatus}");
            Console.WriteLine($"  Min TPS ({config.Thresholds.MinTransactionsPerSecond}): {minTpsStatus}");
            
            Console.WriteLine();
            Console.WriteLine($"Report will be generated at: {config.OutputSettings.HtmlReportPath}");
            Console.WriteLine(new string('=', _consoleWidth));
        }
    }

    private void DisplayCurrentStatus()
    {
        var displayInfo = _metricsCollector.GetConsoleDisplayInfo();
        
        lock (_consoleLock)
        {
            try
            {
                // Save current cursor position
                var originalTop = Console.CursorTop;
                var originalLeft = Console.CursorLeft;
                
                // Move to the status display area
                Console.SetCursorPosition(0, _statusStartLine);
                
                // Build the complete status display as a single string buffer
                var statusLines = new List<string>
                {
                    "LoadRunner Performance Test - Running",
                    new string('=', _consoleWidth),
                    $"Elapsed Time: {displayInfo.ElapsedTime:hh\\:mm\\:ss}",
                    $"Current Load: {displayInfo.CurrentVirtualUsers} virtual users",
                    $"Target TPS: {displayInfo.TargetTPS} | Current TPS: {displayInfo.CurrentTPS:F1}",
                    $"Response Times (ms): P50={displayInfo.P50ResponseTime:F0}, P90={displayInfo.P90ResponseTime:F0}, P95={displayInfo.P95ResponseTime:F0}, P99={displayInfo.P99ResponseTime:F0}",
                    $"Error Rate: {displayInfo.ErrorRate:F1}% ({displayInfo.TotalRequests - displayInfo.SuccessfulRequests:N0}/{displayInfo.TotalRequests:N0} requests)",
                    $"Validation Failures: {displayInfo.ValidationFailureRate:F1}%",
                    $"Memory Usage: {displayInfo.MemoryUsageMB} MB | CPU Usage: {displayInfo.CpuUsagePercent:F1}%",
                    "",
                    "Success Criteria Summary:",
                    $"- HTTP Status: {displayInfo.ValidationSummary.HttpStatusPassRate:F1}% pass rate",
                    $"- Response Time: {displayInfo.ValidationSummary.ResponseTimePassRate:F1}% pass rate",
                    $"- Body Validation: {displayInfo.ValidationSummary.BodyValidationPassRate:F1}% pass rate",
                    $"- Header Validation: {displayInfo.ValidationSummary.HeaderValidationPassRate:F1}% pass rate",
                    "",
                    "Last 10 Transactions:"
                };

                // Add recent transactions
                for (int i = 0; i < Math.Max(10, displayInfo.RecentTransactions.Count); i++)
                {
                    if (i < displayInfo.RecentTransactions.Count)
                    {
                        var transaction = displayInfo.RecentTransactions[i];
                        var statusIcon = transaction.ValidationPassed ? "✓" : "✗";
                        var statusText = transaction.StatusCode >= 200 && transaction.StatusCode < 300 ? "OK" : "ERROR";
                        statusLines.Add($"[{transaction.Timestamp}] {transaction.Method} {transaction.Endpoint} - {transaction.ResponseTimeMs}ms - {transaction.StatusCode} {statusText} - {statusIcon} {transaction.ValidationMessage}");
                    }
                    else
                    {
                        // Add empty lines to maintain consistent display area
                        statusLines.Add(new string(' ', Math.Min(_consoleWidth - 1, 80)));
                    }
                }

                // Output all lines at once to minimize flicker
                for (int i = 0; i < statusLines.Count; i++)
                {
                    try
                    {
                        Console.SetCursorPosition(0, _statusStartLine + i);
                        var line = statusLines[i].PadRight(_consoleWidth - 1).Substring(0, Math.Min(statusLines[i].Length, _consoleWidth - 1));
                        Console.Write(line);
                        Console.Write("\r"); // Ensure we're at the beginning of the line
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Console window might have been resized, continue with next line
                        continue;
                    }
                }
                
                _displayCounter++;
            }
            catch (Exception ex)
            {
                // If console manipulation fails, fall back to simple display
                _logger.LogDebug(ex, "Failed to update console display, falling back to simple output");
                Console.WriteLine($"TPS: {displayInfo.CurrentTPS:F1} | Errors: {displayInfo.ErrorRate:F1}% | Time: {displayInfo.ElapsedTime:hh\\:mm\\:ss}");
            }
        }
    }

}
