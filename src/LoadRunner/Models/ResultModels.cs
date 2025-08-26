namespace LoadRunner.Models;

public class TestExecutionResult
{
    public string RequestName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime Timestamp { get; set; }
    public ValidationResult? ValidationResult { get; set; }
    public string? ErrorMessage { get; set; }
    public int ResponseSizeBytes { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public Dictionary<string, string> RequestHeaders { get; set; } = new();
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
}

public class ValidationResult
{
    public bool IsSuccess { get; set; }
    public List<string> FailureReasons { get; set; } = new();
    public Dictionary<string, object> ValidationDetails { get; set; } = new();
    public TimeSpan ValidationDuration { get; set; }
}

public class LoadTestMetrics
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan ElapsedTime => (EndTime ?? DateTime.UtcNow) - StartTime;
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public int ValidationFailures { get; set; }
    public double ErrorRate => TotalRequests > 0 ? (double)FailedRequests / TotalRequests * 100 : 0;
    public double ValidationFailureRate => TotalRequests > 0 ? (double)ValidationFailures / TotalRequests * 100 : 0;
    public List<double> ResponseTimes { get; set; } = new();
    public int CurrentConcurrentUsers { get; set; }
    public double CurrentTransactionsPerSecond { get; set; }
    
    public double AverageResponseTime => ResponseTimes.Count > 0 ? ResponseTimes.Average() : 0;
    public double MinResponseTime => ResponseTimes.Count > 0 ? ResponseTimes.Min() : 0;
    public double MaxResponseTime => ResponseTimes.Count > 0 ? ResponseTimes.Max() : 0;
    
    public double GetPercentile(double percentile)
    {
        if (ResponseTimes.Count == 0) return 0;
        
        var sorted = ResponseTimes.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(sorted.Count * percentile / 100) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

public class ConsoleDisplayInfo
{
    public TimeSpan ElapsedTime { get; set; }
    public int CurrentVirtualUsers { get; set; }
    public int TargetTPS { get; set; }
    public double CurrentTPS { get; set; }
    public double P50ResponseTime { get; set; }
    public double P90ResponseTime { get; set; }
    public double P95ResponseTime { get; set; }
    public double P99ResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public double ValidationFailureRate { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public long MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public List<RecentTransaction> RecentTransactions { get; set; } = new();
    public ValidationSummary ValidationSummary { get; set; } = new();
}

public class RecentTransaction
{
    public string Timestamp { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public bool ValidationPassed { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
}

public class ValidationSummary
{
    public double HttpStatusPassRate { get; set; }
    public double ResponseTimePassRate { get; set; }
    public double BodyValidationPassRate { get; set; }
    public double HeaderValidationPassRate { get; set; }
}
