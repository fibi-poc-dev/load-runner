using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LoadRunner.Services;

public interface ILoadTestEngine
{
    Task<bool> ExecuteLoadTestAsync(CancellationToken cancellationToken);
}

public class LoadTestEngine : ILoadTestEngine
{
    private readonly IConfigurationManager _configurationManager;
    private readonly IDataProvider _dataProvider;
    private readonly IRequestSequenceManager _sequenceManager;
    private readonly IMetricsCollector _metricsCollector;
    private readonly IConsoleMonitor _consoleMonitor;
    private readonly IReportGenerator _reportGenerator;
    private readonly IFailedRequestLogger _failedRequestLogger;
    private readonly ILogger<LoadTestEngine> _logger;

    private PostmanCollection? _postmanCollection;
    private List<CsvDataRow>? _testData;
    private Dictionary<string, string>? _globalVariables;

    public LoadTestEngine(
        IConfigurationManager configurationManager,
        IDataProvider dataProvider,
        IRequestSequenceManager sequenceManager,
        IMetricsCollector metricsCollector,
        IConsoleMonitor consoleMonitor,
        IReportGenerator reportGenerator,
        IFailedRequestLogger failedRequestLogger,
        ILogger<LoadTestEngine> logger)
    {
        _configurationManager = configurationManager;
        _dataProvider = dataProvider;
        _sequenceManager = sequenceManager;
        _metricsCollector = metricsCollector;
        _consoleMonitor = consoleMonitor;
        _reportGenerator = reportGenerator;
        _failedRequestLogger = failedRequestLogger;
        _logger = logger;
    }

    public async Task<bool> ExecuteLoadTestAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Initialize test components
            await InitializeTestAsync();

            // Set up report directory for failed request logging
            var reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "reports");
            _failedRequestLogger.SetReportDirectory(reportsDirectory);

            var config = _configurationManager.Configuration;
            var testDuration = TimeSpan.FromMilliseconds(config.ExecutionSettings.TestDurationMs);
            var rampUpDuration = TimeSpan.FromMilliseconds(config.ExecutionSettings.RampUpTimeMs);
            var rampDownDuration = TimeSpan.FromMilliseconds(config.ExecutionSettings.RampDownTimeMs);

            _consoleMonitor.DisplayStartMessage();
            await Task.Delay(2000, cancellationToken); // Give user time to read

            // Start metrics collection
            _metricsCollector.StartTest();

            // Start console monitoring
            var monitoringTask = _consoleMonitor.StartMonitoringAsync(cancellationToken);

            // Execute the load test phases
            var testTask = ExecuteTestPhasesAsync(testDuration, rampUpDuration, rampDownDuration, cancellationToken);

            // Wait for either test completion or cancellation
            await Task.WhenAny(testTask, monitoringTask);

            // End metrics collection
            _metricsCollector.EndTest();

            // Generate HTML report
            var finalMetrics = _metricsCollector.GetCurrentMetrics();
            var allResults = _metricsCollector.GetRecentResults(int.MaxValue); // Get all results
            await _reportGenerator.GenerateHtmlReportAsync(finalMetrics, allResults);

            // Display final summary
            _consoleMonitor.DisplaySummary();

            _logger.LogInformation("Load test completed successfully");
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Load test was cancelled by user");
            _metricsCollector.EndTest();
            
            // Generate HTML report even for cancelled tests
            var finalMetrics = _metricsCollector.GetCurrentMetrics();
            var allResults = _metricsCollector.GetRecentResults(int.MaxValue);
            await _reportGenerator.GenerateHtmlReportAsync(finalMetrics, allResults);
            
            _consoleMonitor.DisplaySummary();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load test failed with error");
            Console.WriteLine($"\nLoad test failed: {ex.Message}");
            return false;
        }
    }

    private async Task InitializeTestAsync()
    {
        _logger.LogInformation("Initializing load test components...");

        // Load Postman collection
        _postmanCollection = _configurationManager.LoadPostmanCollection();
        
        // Load test data
        _testData = await _dataProvider.LoadCsvDataAsync();
        
        // Get global variables
        _globalVariables = _dataProvider.GetGlobalVariables();

        _logger.LogInformation("Load test initialization completed");
    }

    private async Task ExecuteTestPhasesAsync(TimeSpan testDuration, TimeSpan rampUpDuration, TimeSpan rampDownDuration, CancellationToken cancellationToken)
    {
        var config = _configurationManager.Configuration;
        var totalDuration = testDuration + rampUpDuration + rampDownDuration;
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation("Starting load test phases: RampUp({RampUp}min) -> Steady({Steady}min) -> RampDown({RampDown}min)",
            rampUpDuration.TotalMinutes, testDuration.TotalMinutes, rampDownDuration.TotalMinutes);

        var userTasks = new ConcurrentBag<Task>();
        var currentPhase = "RampUp";

        while (DateTime.UtcNow - startTime < totalDuration && !cancellationToken.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - startTime;
            var targetUsers = CalculateTargetUsers(elapsed, rampUpDuration, testDuration, rampDownDuration, config.PerformanceSettings.MaxConcurrentUsers);
            
            // Update current phase
            var newPhase = GetCurrentPhase(elapsed, rampUpDuration, testDuration);
            if (newPhase != currentPhase)
            {
                currentPhase = newPhase;
                _logger.LogInformation("Entering {Phase} phase", currentPhase);
            }

            // Adjust user count
            await AdjustUserCountAsync(targetUsers, userTasks, cancellationToken);
            
            // Update metrics
            _metricsCollector.UpdateConcurrentUsers(userTasks.Count(t => !t.IsCompleted));

            // Wait before next adjustment
            await Task.Delay(1000, cancellationToken);
        }

        // Wait for all remaining tasks to complete with timeout
        _logger.LogInformation("Waiting for all user tasks to complete...");
        var incompleteTasks = userTasks.Where(t => !t.IsCompleted).ToList();
        if (incompleteTasks.Any())
        {
            try
            {
                // Wait maximum 10 seconds for tasks to complete gracefully
                await Task.WhenAll(incompleteTasks).WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Some user tasks did not complete within timeout, proceeding with test completion");
            }
        }
        
        _logger.LogInformation("All load test phases completed");
    }

    private int CalculateTargetUsers(TimeSpan elapsed, TimeSpan rampUpDuration, TimeSpan testDuration, TimeSpan rampDownDuration, int maxUsers)
    {
        if (elapsed <= rampUpDuration)
        {
            // Ramp-up phase: gradually increase users
            var rampUpProgress = elapsed.TotalMinutes / rampUpDuration.TotalMinutes;
            return (int)(maxUsers * rampUpProgress);
        }
        else if (elapsed <= rampUpDuration + testDuration)
        {
            // Steady phase: maintain max users
            return maxUsers;
        }
        else
        {
            // Ramp-down phase: gradually decrease users
            var rampDownElapsed = elapsed - rampUpDuration - testDuration;
            var rampDownProgress = 1.0 - (rampDownElapsed.TotalMinutes / rampDownDuration.TotalMinutes);
            return Math.Max(0, (int)(maxUsers * rampDownProgress));
        }
    }

    private string GetCurrentPhase(TimeSpan elapsed, TimeSpan rampUpDuration, TimeSpan testDuration)
    {
        if (elapsed <= rampUpDuration)
            return "RampUp";
        else if (elapsed <= rampUpDuration + testDuration)
            return "Steady";
        else
            return "RampDown";
    }

    private async Task AdjustUserCountAsync(int targetUsers, ConcurrentBag<Task> userTasks, CancellationToken cancellationToken)
    {
        var activeTasks = userTasks.Where(t => !t.IsCompleted).Count();
        
        if (activeTasks < targetUsers)
        {
            // Start new user tasks
            var newUsersToStart = targetUsers - activeTasks;
            for (int i = 0; i < newUsersToStart; i++)
            {
                var userTask = SimulateVirtualUserAsync(cancellationToken);
                userTasks.Add(userTask);
            }
            
            _logger.LogDebug("Started {Count} new virtual users. Active: {Active}, Target: {Target}",
                newUsersToStart, activeTasks + newUsersToStart, targetUsers);
        }
        
        // Note: We don't forcefully stop user tasks as they should naturally complete their iterations
        // The ramp-down happens by not starting new tasks rather than killing existing ones
    }

    private async Task SimulateVirtualUserAsync(CancellationToken cancellationToken)
    {
        var config = _configurationManager.Configuration;
        var enabledSteps = config.ExecutionSettings.IterationSettings.Where(s => s.Enabled).ToList();
        
        if (enabledSteps.Count == 0)
        {
            _logger.LogWarning("No enabled steps found for virtual user");
            return;
        }

        var random = new Random();
        var dataRowIndex = random.Next(_testData!.Count);
        var dataRow = _testData[dataRowIndex];
        var userVariables = _dataProvider.MapRowToVariables(dataRow);
        
        // Merge global and user variables
        var allVariables = new Dictionary<string, string>(_globalVariables!);
        foreach (var kvp in userVariables)
        {
            allVariables[kvp.Key] = kvp.Value;
        }

        _logger.LogDebug("Virtual user started with data row {Index}", dataRowIndex);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var step in enabledSteps)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await ExecuteStepAsync(step, allVariables);
                    
                    // Wait for step interval
                    var intervalMs = step.IntervalMs;
                    await Task.Delay(intervalMs, cancellationToken);
                }
                
                // Add small randomness to prevent thundering herd (max 1 second)
                var jitter = random.Next(0, 1000);
                await Task.Delay(jitter, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Virtual user stopped due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virtual user encountered error");
        }
    }

    private async Task ExecuteStepAsync(IterationSetting step, Dictionary<string, string> variables)
    {
        try
        {
            // Execute the request using sequence manager which handles authentication and dependencies
            var result = await _sequenceManager.ExecuteRequestWithDependenciesAsync(
                step.StepName, 
                variables, 
                step.SuccessCriteria);
            
            // Record the result
            _metricsCollector.RecordResult(result);
            
            _logger.LogDebug("Step {StepName} executed: {StatusCode} in {ResponseTime}ms",
                step.StepName, result.StatusCode, result.ResponseTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute step: {StepName}", step.StepName);
            
            // Record failed result
            var failedResult = new TestExecutionResult
            {
                RequestName = step.StepName,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow,
                ResponseTime = TimeSpan.Zero,
                StatusCode = 0
            };
            _metricsCollector.RecordResult(failedResult);
        }
    }
}
