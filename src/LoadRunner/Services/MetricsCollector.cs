using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LoadRunner.Services;

public interface IMetricsCollector
{
    void RecordResult(TestExecutionResult result);
    LoadTestMetrics GetCurrentMetrics();
    ConsoleDisplayInfo GetConsoleDisplayInfo();
    void StartTest();
    void EndTest();
    void UpdateConcurrentUsers(int count);
    void Reset();
    List<TestExecutionResult> GetRecentResults(int count = 10);
}

public class MetricsCollector : IMetricsCollector
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<MetricsCollector> _logger;
    private readonly ConcurrentQueue<TestExecutionResult> _results = new();
    private readonly ConcurrentQueue<TestExecutionResult> _allResults = new();  // Store all results for reporting
    private readonly object _lockObject = new();
    private LoadTestMetrics _metrics = new();
    private readonly List<TestExecutionResult> _recentResults = new();
    private readonly Process _currentProcess = Process.GetCurrentProcess();

    public MetricsCollector(IConfigurationManager configurationManager, ILogger<MetricsCollector> logger)
    {
        _configurationManager = configurationManager;
        _logger = logger;
        Reset();
    }

    public void StartTest()
    {
        lock (_lockObject)
        {
            _metrics.StartTime = DateTime.UtcNow;
            _metrics.EndTime = null;
            _logger.LogInformation("Load test metrics collection started at {StartTime}", _metrics.StartTime);
        }
    }

    public void EndTest()
    {
        lock (_lockObject)
        {
            _metrics.EndTime = DateTime.UtcNow;
            _logger.LogInformation("Load test metrics collection ended at {EndTime}. Duration: {Duration}", 
                _metrics.EndTime, _metrics.ElapsedTime);
        }
    }

    public void RecordResult(TestExecutionResult result)
    {
        _results.Enqueue(result);
        _allResults.Enqueue(result); // Store for reporting
        
        lock (_lockObject)
        {
            _metrics.TotalRequests++;
            
            if (result.IsSuccess)
            {
                _metrics.SuccessfulRequests++;
            }
            else
            {
                _metrics.FailedRequests++;
            }

            if (result.ValidationResult != null && !result.ValidationResult.IsSuccess)
            {
                _metrics.ValidationFailures++;
            }

            // Add response time
            _metrics.ResponseTimes.Add(result.ResponseTime.TotalMilliseconds);

            // Keep only recent response times to prevent memory issues
            if (_metrics.ResponseTimes.Count > 10000)
            {
                _metrics.ResponseTimes.RemoveRange(0, _metrics.ResponseTimes.Count - 10000);
            }

            // Update recent results
            _recentResults.Add(result);
            if (_recentResults.Count > 50) // Keep last 50 results
            {
                _recentResults.RemoveRange(0, _recentResults.Count - 50);
            }

            // Calculate current TPS
            CalculateCurrentTps();
        }

        _logger.LogDebug("Recorded result: {RequestName} - {StatusCode} in {ResponseTime}ms - Success: {IsSuccess}",
            result.RequestName, result.StatusCode, result.ResponseTime.TotalMilliseconds, result.IsSuccess);
    }

    public LoadTestMetrics GetCurrentMetrics()
    {
        lock (_lockObject)
        {
            // Return a copy to avoid threading issues
            return new LoadTestMetrics
            {
                StartTime = _metrics.StartTime,
                EndTime = _metrics.EndTime,
                TotalRequests = _metrics.TotalRequests,
                SuccessfulRequests = _metrics.SuccessfulRequests,
                FailedRequests = _metrics.FailedRequests,
                ValidationFailures = _metrics.ValidationFailures,
                ResponseTimes = new List<double>(_metrics.ResponseTimes),
                CurrentConcurrentUsers = _metrics.CurrentConcurrentUsers,
                CurrentTransactionsPerSecond = _metrics.CurrentTransactionsPerSecond
            };
        }
    }

    public ConsoleDisplayInfo GetConsoleDisplayInfo()
    {
        lock (_lockObject)
        {
            var metrics = _metrics;
            var config = _configurationManager.Configuration;

            var displayInfo = new ConsoleDisplayInfo
            {
                ElapsedTime = metrics.ElapsedTime,
                CurrentVirtualUsers = metrics.CurrentConcurrentUsers,
                TargetTPS = config.PerformanceSettings.TargetTransactionsPerSecond,
                CurrentTPS = metrics.CurrentTransactionsPerSecond,
                ErrorRate = metrics.ErrorRate,
                ValidationFailureRate = metrics.ValidationFailureRate,
                TotalRequests = metrics.TotalRequests,
                SuccessfulRequests = metrics.SuccessfulRequests,
                MemoryUsageMB = GetMemoryUsageMB(),
                CpuUsagePercent = GetCpuUsagePercent()
            };

            // Calculate percentiles
            if (metrics.ResponseTimes.Count > 0)
            {
                displayInfo.P50ResponseTime = metrics.GetPercentile(50);
                displayInfo.P90ResponseTime = metrics.GetPercentile(90);
                displayInfo.P95ResponseTime = metrics.GetPercentile(95);
                displayInfo.P99ResponseTime = metrics.GetPercentile(99);
            }

            // Get recent transactions
            displayInfo.RecentTransactions = _recentResults
                .TakeLast(10)
                .Select(r => new RecentTransaction
                {
                    Timestamp = r.Timestamp.ToString("HH:mm:ss"),
                    Method = r.Method,
                    Endpoint = GetShortEndpoint(r.Url),
                    ResponseTimeMs = (int)r.ResponseTime.TotalMilliseconds,
                    StatusCode = r.StatusCode,
                    ValidationPassed = r.ValidationResult?.IsSuccess ?? true,
                    ValidationMessage = r.ValidationResult?.IsSuccess == false 
                        ? GetFirstValidationFailure(r.ValidationResult)
                        : "All validations passed"
                })
                .ToList();

            // Calculate validation summary
            displayInfo.ValidationSummary = CalculateValidationSummary();

            return displayInfo;
        }
    }

    public void UpdateConcurrentUsers(int count)
    {
        lock (_lockObject)
        {
            _metrics.CurrentConcurrentUsers = count;
        }
    }

    public void Reset()
    {
        lock (_lockObject)
        {
            _metrics = new LoadTestMetrics();
            _recentResults.Clear();
            
            // Clear the results queue
            while (_results.TryDequeue(out _)) { }
            
            _logger.LogInformation("Metrics collector reset");
        }
    }

    public List<TestExecutionResult> GetRecentResults(int count = 10)
    {
        if (count == int.MaxValue)
        {
            // Return all results for reporting
            return _allResults.ToList();
        }
        
        lock (_lockObject)
        {
            return _recentResults.TakeLast(count).ToList();
        }
    }

    private void CalculateCurrentTps()
    {
        // Calculate TPS based on requests in the last 10 seconds
        var tenSecondsAgo = DateTime.UtcNow.AddSeconds(-10);
        var recentRequests = _recentResults.Count(r => r.Timestamp >= tenSecondsAgo);
        _metrics.CurrentTransactionsPerSecond = recentRequests / 10.0;
    }

    private long GetMemoryUsageMB()
    {
        try
        {
            _currentProcess.Refresh();
            return _currentProcess.WorkingSet64 / (1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }

    private double GetCpuUsagePercent()
    {
        try
        {
            // This is a simplified CPU calculation
            // In a real implementation, you'd want to calculate this over time
            _currentProcess.Refresh();
            return _currentProcess.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0;
        }
        catch
        {
            return 0;
        }
    }

    private string GetShortEndpoint(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            
            // Return last 2 path segments for brevity
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                return $".../{segments[^2]}/{segments[^1]}";
            }
            return segments.Length > 0 ? segments[^1] : path;
        }
        catch
        {
            return url.Length > 30 ? "..." + url.Substring(url.Length - 27) : url;
        }
    }

    private string GetFirstValidationFailure(ValidationResult validationResult)
    {
        if (validationResult.FailureReasons.Count > 0)
        {
            var failure = validationResult.FailureReasons[0];
            return failure.Length > 50 ? failure.Substring(0, 47) + "..." : failure;
        }
        return "Validation failed";
    }

    private ValidationSummary CalculateValidationSummary()
    {
        var totalResults = _recentResults.Count;
        if (totalResults == 0)
        {
            return new ValidationSummary
            {
                HttpStatusPassRate = 100,
                ResponseTimePassRate = 100,
                BodyValidationPassRate = 100,
                HeaderValidationPassRate = 100
            };
        }

        var httpStatusPassed = _recentResults.Count(r => r.StatusCode >= 200 && r.StatusCode < 300);
        var responseTimePassed = _recentResults.Count(r => 
            r.ResponseTime.TotalMilliseconds <= _configurationManager.Configuration.Thresholds.MaxResponseTimeMs);
        var validationPassed = _recentResults.Count(r => r.ValidationResult?.IsSuccess != false);
        
        return new ValidationSummary
        {
            HttpStatusPassRate = (double)httpStatusPassed / totalResults * 100,
            ResponseTimePassRate = (double)responseTimePassed / totalResults * 100,
            BodyValidationPassRate = (double)validationPassed / totalResults * 100,
            HeaderValidationPassRate = (double)validationPassed / totalResults * 100
        };
    }
}
