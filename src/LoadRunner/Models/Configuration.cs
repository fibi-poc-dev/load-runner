namespace LoadRunner.Models;

public class LoadRunnerConfiguration
{
    public string PostmanCollectionPath { get; set; } = string.Empty;
    public string CsvDataPath { get; set; } = string.Empty;
    public string ColumnMappingPath { get; set; } = string.Empty;
    public OutputSettings OutputSettings { get; set; } = new();
    public ExecutionSettings ExecutionSettings { get; set; } = new();
    public PerformanceSettings PerformanceSettings { get; set; } = new();
    public ThresholdSettings Thresholds { get; set; } = new();
    public GlobalSuccessCriteria GlobalSuccessCriteria { get; set; } = new();
}

public class OutputSettings
{
    public string HtmlReportPath { get; set; } = string.Empty;
    public int ConsoleUpdateIntervalMs { get; set; } = 1000;
}

public class ExecutionSettings
{
    public int TestDurationMs { get; set; }
    public int RampUpTimeMs { get; set; }
    public int RampDownTimeMs { get; set; }
    public List<IterationSetting> IterationSettings { get; set; } = new();
}

public class IterationSetting
{
    public string StepName { get; set; } = string.Empty;
    public int IntervalMs { get; set; }
    public bool Enabled { get; set; } = true;
    public SuccessCriteria? SuccessCriteria { get; set; }
}

public class PerformanceSettings
{
    public int TargetTransactionsPerSecond { get; set; }
    public int MaxConcurrentUsers { get; set; }
    public int RequestTimeoutMs { get; set; } = 30000;
    public int MaxRetries { get; set; } = 3;
}

public class ThresholdSettings
{
    public int MaxResponseTimeMs { get; set; }
    public double MaxErrorRatePercent { get; set; }
    public int MinTransactionsPerSecond { get; set; }
}

public class GlobalSuccessCriteria
{
    public int[] DefaultHttpStatusCodes { get; set; } = { 200, 201, 202, 204 };
    public int DefaultResponseTimeMaxMs { get; set; } = 5000;
    public bool IgnoreSslErrors { get; set; }
    public bool FollowRedirects { get; set; } = true;
    public int MaxRedirects { get; set; } = 5;
}

public class SuccessCriteria
{
    public int[]? HttpStatusCodes { get; set; }
    public int? ResponseTimeMaxMs { get; set; }
    public string? ResponseBodyRegex { get; set; }
    public string[]? ResponseBodyContains { get; set; }
    public ResponseHeaderCheck[]? ResponseHeaderChecks { get; set; }
    public JsonPathValidation[]? JsonPathValidations { get; set; }
    public int? ResponseSizeMinBytes { get; set; }
    public int? ResponseSizeMaxBytes { get; set; }
}

public class ResponseHeaderCheck
{
    public string HeaderName { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string ValidationRule { get; set; } = "NotNull"; // NotNull, Equals, Contains, Regex
}

public class JsonPathValidation
{
    public string JsonPath { get; set; } = string.Empty;
    public string ValidationRule { get; set; } = string.Empty; // NotNull, IsNumeric, IsString, Equals, GreaterThan, LessThan, Regex
    public string? ExpectedValue { get; set; }
}
