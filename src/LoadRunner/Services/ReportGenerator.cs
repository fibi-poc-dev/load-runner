using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LoadRunner.Services;

public interface IReportGenerator
{
    Task GenerateHtmlReportAsync(LoadTestMetrics metrics, List<TestExecutionResult> results);
}

public class ReportGenerator : IReportGenerator
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<ReportGenerator> _logger;

    public ReportGenerator(IConfigurationManager configurationManager, ILogger<ReportGenerator> logger)
    {
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public async Task GenerateHtmlReportAsync(LoadTestMetrics metrics, List<TestExecutionResult> results)
    {
        try
        {
            var config = _configurationManager.Configuration;
            var reportPath = config.OutputSettings.HtmlReportPath;
            
            _logger.LogInformation("Generating HTML report at: {ReportPath}", reportPath);

            var html = GenerateHtmlContent(metrics, results, config);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(reportPath, html);
            
            _logger.LogInformation("HTML report generated successfully at: {ReportPath}", reportPath);
            
            // Open the report in the default browser
            OpenReportInBrowser(reportPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate HTML report");
            throw;
        }
    }

    private string GenerateHtmlContent(LoadTestMetrics metrics, List<TestExecutionResult> results, LoadRunnerConfiguration config)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("    <title>LoadRunner Performance Test Report</title>");
        html.AppendLine("    <style>");
        html.AppendLine(GetCssStyles());
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Header
        html.AppendLine("    <div class=\"header\">");
        html.AppendLine("        <h1>LoadRunner Performance Test Report</h1>");
        html.AppendLine($"        <p>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine("    </div>");
        
        // Executive Summary
        html.AppendLine("    <div class=\"container\">");
        html.AppendLine("        <div class=\"section\">");
        html.AppendLine("            <h2>Executive Summary</h2>");
        html.AppendLine(GenerateExecutiveSummary(metrics, config));
        html.AppendLine("        </div>");
        
        // Performance Metrics
        html.AppendLine("        <div class=\"section\">");
        html.AppendLine("            <h2>Performance Metrics</h2>");
        html.AppendLine(GeneratePerformanceMetrics(metrics));
        html.AppendLine("        </div>");
        
        // Per-API Breakdown
        html.AppendLine("        <div class=\"section\">");
        html.AppendLine("            <h2>Per-API Performance Breakdown</h2>");
        html.AppendLine(GenerateApiBreakdown(results));
        html.AppendLine("        </div>");
        
        // Success Criteria Analysis
        html.AppendLine("        <div class=\"section\">");
        html.AppendLine("            <h2>Success Criteria Analysis</h2>");
        html.AppendLine(GenerateSuccessCriteriaAnalysis(results));
        html.AppendLine("        </div>");
        
        // Error Analysis
        if (results.Any(r => !r.IsSuccess))
        {
            html.AppendLine("        <div class=\"section\">");
            html.AppendLine("            <h2>Error Analysis</h2>");
            html.AppendLine(GenerateErrorAnalysis(results));
            html.AppendLine("        </div>");
        }
        
        // Test Configuration
        html.AppendLine("        <div class=\"section\">");
        html.AppendLine("            <h2>Test Configuration</h2>");
        html.AppendLine(GenerateTestConfiguration(config));
        html.AppendLine("        </div>");
        
        html.AppendLine("    </div>");
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    private string GetCssStyles()
    {
        return @"
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 0;
            background-color: #f5f5f5;
        }
        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 2rem;
            text-align: center;
        }
        .header h1 {
            margin: 0;
            font-size: 2.5rem;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 2rem;
        }
        .section {
            background: white;
            margin: 2rem 0;
            padding: 2rem;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        .section h2 {
            color: #333;
            border-bottom: 3px solid #667eea;
            padding-bottom: 0.5rem;
        }
        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 1rem;
            margin: 1rem 0;
        }
        .metric-card {
            background: #f8f9fa;
            padding: 1rem;
            border-radius: 5px;
            border-left: 4px solid #667eea;
        }
        .metric-value {
            font-size: 1.5rem;
            font-weight: bold;
            color: #333;
        }
        .metric-label {
            color: #666;
            font-size: 0.9rem;
        }
        .status-pass {
            color: #28a745;
            font-weight: bold;
        }
        .status-fail {
            color: #dc3545;
            font-weight: bold;
        }
        .chart-container {
            position: relative;
            height: 400px;
            margin: 2rem 0;
            background: #fff;
            border: 1px solid #e9ecef;
            border-radius: 8px;
            padding: 1rem;
            box-shadow: 0 1px 3px rgba(0,0,0,0.05);
        }
        .chart-container h3 {
            margin: 0 0 1rem 0;
            color: #495057;
            font-size: 1.1rem;
            text-align: center;
            border-bottom: 1px solid #e9ecef;
            padding-bottom: 0.5rem;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin: 1rem 0;
        }
        th, td {
            padding: 0.75rem;
            text-align: left;
            border-bottom: 1px solid #ddd;
        }
        th {
            background-color: #f8f9fa;
            font-weight: 600;
        }
        .config-item {
            margin: 0.5rem 0;
            padding: 0.5rem;
            background: #f8f9fa;
            border-radius: 3px;
        }
        .api-breakdown-container {
            margin: 1rem 0;
        }
        .api-summary-table {
            margin-bottom: 2rem;
        }
        .api-summary-table h3 {
            color: #495057;
            margin-bottom: 1rem;
            font-size: 1.25rem;
        }
        .metrics-table {
            width: 100%;
            border-collapse: collapse;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
            border-radius: 6px;
            overflow: hidden;
        }
        .metrics-table thead th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            font-weight: 600;
            text-align: center;
            padding: 1rem 0.75rem;
        }
        .metrics-table tbody td {
            text-align: center;
            padding: 0.75rem;
            border-bottom: 1px solid #e9ecef;
        }
        .metrics-table tbody tr:hover {
            background-color: #f8f9fa;
        }
        .endpoint-name {
            text-align: left !important;
            font-weight: 600;
            color: #495057;
        }
        .success {
            color: #28a745;
            font-weight: 600;
        }
        .warning {
            color: #ffc107;
            font-weight: 600;
        }
        .error {
            color: #dc3545;
            font-weight: 600;
        }
        .api-details {
            margin-top: 2rem;
        }
        .api-details h3 {
            color: #495057;
            margin-bottom: 1.5rem;
            font-size: 1.25rem;
        }
        .api-detail-card {
            background: #fff;
            border: 1px solid #e9ecef;
            border-radius: 8px;
            margin-bottom: 1.5rem;
            padding: 1.5rem;
            box-shadow: 0 1px 3px rgba(0,0,0,0.05);
        }
        .api-detail-card h4 {
            color: #495057;
            margin: 0 0 1rem 0;
            font-size: 1.1rem;
            padding-bottom: 0.5rem;
            border-bottom: 2px solid #667eea;
        }
        .api-stats {
            display: flex;
            flex-wrap: wrap;
            gap: 1rem;
        }
        .stat-item {
            display: flex;
            flex-direction: column;
            padding: 0.75rem;
            background: #f8f9fa;
            border-radius: 6px;
            min-width: 150px;
        }
        .stat-item.error {
            background: #f8d7da;
            border-left: 4px solid #dc3545;
        }
        .stat-item.warning {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
        }
        .stat-label {
            font-size: 0.875rem;
            color: #6c757d;
            margin-bottom: 0.25rem;
        }
        .stat-value {
            font-size: 1.1rem;
            font-weight: 600;
            color: #495057;
        }
        .error-detail {
            margin-top: 0.5rem;
            padding: 0.5rem;
            background: #ffffff;
            border-radius: 4px;
            font-size: 0.875rem;
            color: #721c24;
        }
        ";
    }

    private string GetPlotlyLibrary()
    {
        // Plotly.js - embedded for offline functionality
        var plotlyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plotly.min.js");
        
        if (File.Exists(plotlyPath))
        {
            return File.ReadAllText(plotlyPath);
        }
        
        // Fallback: return a minimal Plotly.js library loader inline
        return GetPlotlyInline();
    }

    private string GetPlotlyInline()
    {
        // This is the Plotly.js library minified for offline use
        // Due to size constraints, we'll load from embedded resource or use a smaller fallback
        try
        {
            var plotlyPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "plotly.min.js");
            if (File.Exists(plotlyPath))
            {
                return File.ReadAllText(plotlyPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load Plotly.js from file, using CDN fallback");
        }
        
        // If file loading fails, return a placeholder that will load from CDN as fallback
        return @"
            /* Plotly.js fallback loader */
            (function() {
                if (typeof Plotly === 'undefined') {
                    var script = document.createElement('script');
                    script.src = 'https://cdn.plot.ly/plotly-latest.min.js';
                    script.onload = function() {
                        console.log('Plotly.js loaded from CDN as fallback');
                    };
                    script.onerror = function() {
                        console.error('Failed to load Plotly.js from CDN');
                        document.body.innerHTML += '<div style=""background: #ff6b6b; color: white; padding: 1rem; margin: 1rem; border-radius: 4px;"">Plotly.js library failed to load. Charts will not be displayed.</div>';
                    };
                    document.head.appendChild(script);
                }
            })();
        ";
    }

    private string GenerateExecutiveSummary(LoadTestMetrics metrics, LoadRunnerConfiguration config)
    {
        var html = new StringBuilder();
        
        var overallStatus = DetermineOverallStatus(metrics, config);
        var statusClass = overallStatus ? "status-pass" : "status-fail";
        var statusText = overallStatus ? "PASS" : "FAIL";
        
        html.AppendLine("<div class=\"metrics-grid\">");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value {statusClass}\">{statusText}</div>");
        html.AppendLine($"        <div class=\"metric-label\">Overall Status</div>");
        html.AppendLine($"    </div>");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{metrics.ElapsedTime:hh\\:mm\\:ss}</div>");
        html.AppendLine($"        <div class=\"metric-label\">Test Duration</div>");
        html.AppendLine($"    </div>");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{metrics.TotalRequests:N0}</div>");
        html.AppendLine($"        <div class=\"metric-label\">Total Requests</div>");
        html.AppendLine($"    </div>");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{metrics.CurrentTransactionsPerSecond:F1}</div>");
        html.AppendLine($"        <div class=\"metric-label\">Average TPS</div>");
        html.AppendLine($"    </div>");
        html.AppendLine("</div>");
        
        return html.ToString();
    }

    private string GeneratePerformanceMetrics(LoadTestMetrics metrics)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<div class=\"metrics-grid\">");
        
        if (metrics.ResponseTimes.Count > 0)
        {
            html.AppendLine($"    <div class=\"metric-card\">");
            html.AppendLine($"        <div class=\"metric-value\">{metrics.AverageResponseTime:F0}ms</div>");
            html.AppendLine($"        <div class=\"metric-label\">Average Response Time</div>");
            html.AppendLine($"    </div>");
            html.AppendLine($"    <div class=\"metric-card\">");
            html.AppendLine($"        <div class=\"metric-value\">{metrics.GetPercentile(50):F0}ms</div>");
            html.AppendLine($"        <div class=\"metric-label\">P50 Response Time</div>");
            html.AppendLine($"    </div>");
            html.AppendLine($"    <div class=\"metric-card\">");
            html.AppendLine($"        <div class=\"metric-value\">{metrics.GetPercentile(95):F0}ms</div>");
            html.AppendLine($"        <div class=\"metric-label\">P95 Response Time</div>");
            html.AppendLine($"    </div>");
            html.AppendLine($"    <div class=\"metric-card\">");
            html.AppendLine($"        <div class=\"metric-value\">{metrics.GetPercentile(99):F0}ms</div>");
            html.AppendLine($"        <div class=\"metric-label\">P99 Response Time</div>");
            html.AppendLine($"    </div>");
        }
        
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{metrics.ErrorRate:F1}%</div>");
        html.AppendLine($"        <div class=\"metric-label\">Error Rate</div>");
        html.AppendLine($"    </div>");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{metrics.ValidationFailureRate:F1}%</div>");
        html.AppendLine($"        <div class=\"metric-label\">Validation Failure Rate</div>");
        html.AppendLine($"    </div>");
        html.AppendLine("</div>");
        
        return html.ToString();
    }

    private string GenerateSuccessCriteriaAnalysis(List<TestExecutionResult> results)
    {
        var html = new StringBuilder();
        
        var totalResults = results.Count;
        if (totalResults == 0)
        {
            html.AppendLine("<p>No results available for analysis.</p>");
            return html.ToString();
        }
        
        var httpStatusPassed = results.Count(r => r.StatusCode >= 200 && r.StatusCode < 300);
        var validationPassed = results.Count(r => r.ValidationResult?.IsSuccess != false);
        
        html.AppendLine("<div class=\"metrics-grid\">");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{(double)httpStatusPassed / totalResults * 100:F1}%</div>");
        html.AppendLine($"        <div class=\"metric-label\">HTTP Status Success Rate</div>");
        html.AppendLine($"    </div>");
        html.AppendLine($"    <div class=\"metric-card\">");
        html.AppendLine($"        <div class=\"metric-value\">{(double)validationPassed / totalResults * 100:F1}%</div>");
        html.AppendLine($"        <div class=\"metric-label\">Validation Success Rate</div>");
        html.AppendLine($"    </div>");
        html.AppendLine("</div>");
        
        return html.ToString();
    }

    private string GenerateErrorAnalysis(List<TestExecutionResult> results)
    {
        var html = new StringBuilder();
        var errors = results.Where(r => !r.IsSuccess).ToList();
        
        if (!errors.Any())
        {
            html.AppendLine("<p>No errors encountered during the test.</p>");
            return html.ToString();
        }
        
        html.AppendLine("<table>");
        html.AppendLine("<thead>");
        html.AppendLine("    <tr><th>Request</th><th>Error</th><th>Status Code</th><th>Count</th></tr>");
        html.AppendLine("</thead>");
        html.AppendLine("<tbody>");
        
        var errorGroups = errors
            .GroupBy(e => new { e.RequestName, e.StatusCode, ErrorMessage = e.ErrorMessage ?? "Unknown error" })
            .OrderByDescending(g => g.Count())
            .Take(10);
        
        foreach (var group in errorGroups)
        {
            html.AppendLine($"    <tr>");
            html.AppendLine($"        <td>{group.Key.RequestName}</td>");
            html.AppendLine($"        <td>{group.Key.ErrorMessage}</td>");
            html.AppendLine($"        <td>{group.Key.StatusCode}</td>");
            html.AppendLine($"        <td>{group.Count()}</td>");
            html.AppendLine($"    </tr>");
        }
        
        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
        
        return html.ToString();
    }

    private string GenerateTestConfiguration(LoadRunnerConfiguration config)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<div class=\"config-item\"><strong>Test Duration:</strong> " + (config.ExecutionSettings.TestDurationMs / 1000.0) + " seconds</div>");
        html.AppendLine("<div class=\"config-item\"><strong>Ramp-up Duration:</strong> " + (config.ExecutionSettings.RampUpTimeMs / 1000.0) + " seconds</div>");
        html.AppendLine("<div class=\"config-item\"><strong>Target TPS:</strong> " + config.PerformanceSettings.TargetTransactionsPerSecond + "</div>");
        html.AppendLine("<div class=\"config-item\"><strong>Max Concurrent Users:</strong> " + config.PerformanceSettings.MaxConcurrentUsers + "</div>");
        html.AppendLine("<div class=\"config-item\"><strong>Request Timeout:</strong> " + config.PerformanceSettings.RequestTimeoutMs + "ms</div>");
        
        return html.ToString();
    }

    private string GenerateChartScript(List<TestExecutionResult> results)
    {
        // Sample data for charts (max 100 points for better performance and readability)
        var maxDataPoints = 100;
        var sampledResults = results.Count > maxDataPoints 
            ? results.Where((r, i) => i % (results.Count / maxDataPoints) == 0).ToList()
            : results;
            
        var responseTimeData = JsonSerializer.Serialize(
            sampledResults.Select((r, index) => new { x = index, y = r.ResponseTime.TotalMilliseconds }).ToList());

        // Group results by API for per-API charts
        var apiGroups = results.GroupBy(r => r.RequestName).ToList();
        
        // Prepare data for API-specific charts
        var apiNames = apiGroups.Select(g => g.Key).ToList();
        var apiColors = new[] { "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40", "#FF6384", "#C9CBCF" };
        
        // API Response Time Data
        var apiResponseTimeData = apiGroups.Select((g, index) => new
        {
            label = g.Key,
            data = new[] { g.Average(r => r.ResponseTime.TotalMilliseconds) },
            backgroundColor = apiColors[index % apiColors.Length]
        }).ToList();
        
        // API Success Rate Data
        var apiSuccessData = apiGroups.Select((g, index) => new
        {
            label = g.Key,
            data = new[] { (double)g.Count(r => r.IsSuccess) / g.Count() * 100 },
            backgroundColor = apiColors[index % apiColors.Length]
        }).ToList();
        
        // API Distribution Data
        var apiDistributionData = apiGroups.Select((g, index) => new
        {
            label = g.Key,
            data = g.Count(),
            backgroundColor = apiColors[index % apiColors.Length]
        }).ToList();
        
        // Spider Chart Data - normalized metrics (0-100 scale)
        var spiderData = apiGroups.Select((g, index) => 
        {
            var responseTimes = g.Select(r => r.ResponseTime.TotalMilliseconds).ToList();
            var avgResponseTime = responseTimes.Average();
            var successRate = (double)g.Count(r => r.IsSuccess) / g.Count() * 100;
            var validationRate = g.Where(r => r.ValidationResult != null).Any() 
                ? (double)g.Count(r => r.ValidationResult?.IsSuccess == true) / g.Count(r => r.ValidationResult != null) * 100 
                : 100;
            
            // Normalize response time (assuming 100ms is baseline good, 0ms is perfect)
            var normalizedResponseTime = Math.Max(0, 100 - Math.Min(100, avgResponseTime));
            
            return new
            {
                label = g.Key,
                data = new[] { normalizedResponseTime, successRate, validationRate, (double)g.Count() / results.Count * 100 },
                backgroundColor = $"rgba({GetRgbFromHex(apiColors[index % apiColors.Length])}, 0.2)",
                borderColor = apiColors[index % apiColors.Length],
                borderWidth = 2
            };
        }).ToList();

        return $@"
        document.addEventListener('DOMContentLoaded', function() {{
            // Initialize all charts after DOM is ready
            console.log('DOM loaded, checking Plotly.js availability...');
            
            if (typeof Plotly === 'undefined') {{
                console.error('Plotly.js is not loaded! Charts will not be displayed.');
                return;
            }}
            
            console.log('Plotly.js is available, initializing charts...');
            
            // Check if all required DOM elements exist
            const requiredElements = ['responseTimeChart', 'throughputChart', 'apiResponseTimeChart', 'spiderChart', 'apiDistributionChart', 'apiSuccessChart'];
            const missingElements = requiredElements.filter(id => !document.getElementById(id));
            
            if (missingElements.length > 0) {{
                console.error('Missing chart container elements:', missingElements);
                return;
            }}
            
            console.log('All chart containers found, creating charts...');
            
            // Response Time Chart
            try {{
                console.log('Creating Response Time Chart...');
                const responseTimeChartData = [{{{responseTimeData}}}];
                const plotData = [{{
                    x: responseTimeChartData.map(d => d.x),
                    y: responseTimeChartData.map(d => d.y),
                    type: 'scatter',
                    mode: 'lines',
                    name: 'Response Time (ms)',
                    line: {{ color: '#667eea', width: 2 }},
                    fill: 'tonexty',
                    fillcolor: 'rgba(102, 126, 234, 0.1)'
                }}];
                
                const layout = {{
                    title: {{ text: '', font: {{ size: 16 }} }},
                    xaxis: {{ title: 'Time (seconds)', showgrid: true }},
                    yaxis: {{ title: 'Response Time (ms)', showgrid: true }},
                    margin: {{ t: 40, r: 40, b: 60, l: 80 }},
                    showlegend: false,
                    plot_bgcolor: 'rgba(0,0,0,0)',
                    paper_bgcolor: 'rgba(0,0,0,0)'
                }};
                
                const config = {{ responsive: true, displayModeBar: false }};
                
                Plotly.newPlot('responseTimeChart', plotData, layout, config);
            console.log('Response Time Chart created successfully!');
            }} catch (error) {{
                console.error('Failed to create Response Time Chart:', error);
            }}

        // Throughput Chart
        try {{
            console.log('Creating Throughput Chart...');
        const throughputData = [];
        let currentMinute = '';
        let requestCount = 0;
        let minuteIndex = 0;
        
        {JsonSerializer.Serialize(results.Select(r => r.Timestamp.ToString("HH:mm")).ToList())}.forEach(time => {{
            if (time !== currentMinute) {{
                if (currentMinute !== '') {{
                    throughputData.push({{ x: minuteIndex, y: requestCount }});
                    minuteIndex++;
                }}
                currentMinute = time;
                requestCount = 1;
            }} else {{
                requestCount++;
            }}
        }});
        if (currentMinute !== '') {{
            throughputData.push({{ x: minuteIndex, y: requestCount }});
        }}
        
        const plotData = [{{
            x: throughputData.map(d => d.x),
            y: throughputData.map(d => d.y),
            type: 'bar',
            name: 'Requests per Minute',
            marker: {{ color: '#28a745' }}
        }}];
        
        const layout = {{
            title: {{ text: '', font: {{ size: 16 }} }},
            xaxis: {{ title: 'Time (minutes)', showgrid: true }},
            yaxis: {{ title: 'Requests per Minute', showgrid: true }},
            margin: {{ t: 40, r: 40, b: 60, l: 80 }},
            showlegend: false,
            plot_bgcolor: 'rgba(0,0,0,0)',
            paper_bgcolor: 'rgba(0,0,0,0)'
        }};
        
        const config = {{ responsive: true, displayModeBar: false }};
        
        Plotly.newPlot('throughputChart', plotData, layout, config);
            console.log('Throughput Chart created successfully!');
        }} catch (error) {{
            console.error('Failed to create Throughput Chart:', error);
        }}

        // API Response Time Comparison Chart
        try {{
            console.log('Creating API Response Time Chart...');
        const plotData = [{{
            x: {JsonSerializer.Serialize(apiNames)},
            y: {JsonSerializer.Serialize(apiResponseTimeData.Select(d => d.data[0]).ToList())},
            type: 'bar',
            name: 'Average Response Time (ms)',
            marker: {{ color: {JsonSerializer.Serialize(apiResponseTimeData.Select(d => d.backgroundColor).ToList())} }}
        }}];
        
        const layout = {{
            title: {{ text: '', font: {{ size: 16 }} }},
            xaxis: {{ title: 'API Endpoint', showgrid: true }},
            yaxis: {{ title: 'Average Response Time (ms)', showgrid: true }},
            margin: {{ t: 40, r: 40, b: 100, l: 80 }},
            showlegend: false,
            plot_bgcolor: 'rgba(0,0,0,0)',
            paper_bgcolor: 'rgba(0,0,0,0)'
        }};
        
        const config = {{ responsive: true, displayModeBar: false }};
        
        Plotly.newPlot('apiResponseTimeChart', plotData, layout, config);
            console.log('API Response Time Chart created successfully!');
        }} catch (error) {{
            console.error('Failed to create API Response Time Chart:', error);
        }}

        // Spider Chart (Radar Chart)
        try {{
            console.log('Creating Spider Chart...');
        const spiderDatasets = {JsonSerializer.Serialize(spiderData)};
        const plotData = spiderDatasets.map(dataset => ({{
            type: 'scatterpolar',
            r: dataset.data.concat([dataset.data[0]]), // Close the shape
            theta: ['Response Time Score', 'Success Rate (%)', 'Validation Rate (%)', 'Traffic Share (%)', 'Response Time Score'],
            fill: 'toself',
            name: dataset.label,
            line: {{ color: dataset.borderColor }},
            fillcolor: dataset.backgroundColor
        }}));
        
        const layout = {{
            title: 'API Performance Overview',
            polar: {{
                radialaxis: {{
                    visible: true,
                    range: [0, 100],
                    tickmode: 'linear',
                    tick0: 0,
                    dtick: 20
                }}
            }},
            margin: {{ t: 80, r: 40, b: 40, l: 40 }},
            plot_bgcolor: 'rgba(0,0,0,0)',
            paper_bgcolor: 'rgba(0,0,0,0)'
        }};
        
        const config = {{ responsive: true, displayModeBar: false }};
        
        Plotly.newPlot('spiderChart', plotData, layout, config);
            console.log('Spider Chart created successfully!');
        }} catch (error) {{
            console.error('Failed to create Spider Chart:', error);
        }}

        // API Distribution Pie Chart
        try {{
            console.log('Creating API Distribution Chart...');
        const plotData = [{{
            values: {JsonSerializer.Serialize(apiDistributionData.Select(d => d.data).ToList())},
            labels: {JsonSerializer.Serialize(apiNames)},
            type: 'pie',
            hole: 0.4, // Makes it a doughnut chart
            marker: {{
                colors: {JsonSerializer.Serialize(apiDistributionData.Select(d => d.backgroundColor).ToList())},
                line: {{ color: '#fff', width: 2 }}
            }}
        }}];
        
        const layout = {{
            title: {{ text: '', font: {{ size: 16 }} }},
            margin: {{ t: 40, r: 40, b: 40, l: 40 }},
            showlegend: true,
            legend: {{ orientation: 'h', y: -0.2, x: 0.5, xanchor: 'center' }},
            plot_bgcolor: 'rgba(0,0,0,0)',
            paper_bgcolor: 'rgba(0,0,0,0)'
        }};
        
        const config = {{ responsive: true, displayModeBar: false }};
        
        Plotly.newPlot('apiDistributionChart', plotData, layout, config);
            console.log('API Distribution Chart created successfully!');
        }} catch (error) {{
            console.error('Failed to create API Distribution Chart:', error);
        }}

        // API Success Rate Chart
        try {{
            console.log('Creating API Success Rate Chart...');
        const plotData = [{{
            x: {JsonSerializer.Serialize(apiNames)},
            y: {JsonSerializer.Serialize(apiSuccessData.Select(d => d.data[0]).ToList())},
            type: 'bar',
            name: 'Success Rate (%)',
            marker: {{ color: {JsonSerializer.Serialize(apiSuccessData.Select(d => d.backgroundColor).ToList())} }}
        }}];
        
        const layout = {{
            title: {{ text: '', font: {{ size: 16 }} }},
            xaxis: {{ title: 'API Endpoint', showgrid: true }},
            yaxis: {{ title: 'Success Rate (%)', showgrid: true, range: [0, 100] }},
            margin: {{ t: 40, r: 40, b: 100, l: 80 }},
            showlegend: false,
            plot_bgcolor: 'rgba(0,0,0,0)',
            paper_bgcolor: 'rgba(0,0,0,0)'
        }};
        
        const config = {{ responsive: true, displayModeBar: false }};
        
        Plotly.newPlot('apiSuccessChart', plotData, layout, config);
            console.log('API Success Rate Chart created successfully!');
        }} catch (error) {{
            console.error('Failed to create API Success Rate Chart:', error);
        }}
        
        console.log('All charts initialized successfully!');
        }}); // End DOMContentLoaded event listener
        ";
    }

    private string GetRgbFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return $"{r}, {g}, {b}";
        }
        return "0, 0, 0";
    }

    private bool DetermineOverallStatus(LoadTestMetrics metrics, LoadRunnerConfiguration config)
    {
        if (metrics.ResponseTimes.Count == 0) return false;
        
        var p95ResponseTime = metrics.GetPercentile(95);
        var responseTimePass = p95ResponseTime <= config.Thresholds.MaxResponseTimeMs;
        var errorRatePass = metrics.ErrorRate <= config.Thresholds.MaxErrorRatePercent;
        var tpsPass = metrics.CurrentTransactionsPerSecond >= config.Thresholds.MinTransactionsPerSecond;
        
        return responseTimePass && errorRatePass && tpsPass;
    }

    private string GenerateApiBreakdown(List<TestExecutionResult> results)
    {
        var html = new StringBuilder();

        // Group results by API endpoint
        var apiGroups = results.GroupBy(r => r.RequestName).ToList();

        if (!apiGroups.Any())
        {
            html.AppendLine("            <p>No API data available.</p>");
            return html.ToString();
        }

        html.AppendLine("            <div class=\"api-breakdown-container\">");
        
        // Summary table
        html.AppendLine("                <div class=\"api-summary-table\">");
        html.AppendLine("                    <h3>API Performance Summary</h3>");
        html.AppendLine("                    <table class=\"metrics-table\">");
        html.AppendLine("                        <thead>");
        html.AppendLine("                            <tr>");
        html.AppendLine("                                <th>API Endpoint</th>");
        html.AppendLine("                                <th>Total Requests</th>");
        html.AppendLine("                                <th>Success Rate</th>");
        html.AppendLine("                                <th>Avg Response Time</th>");
        html.AppendLine("                                <th>P50</th>");
        html.AppendLine("                                <th>P95</th>");
        html.AppendLine("                                <th>P99</th>");
        html.AppendLine("                                <th>Min</th>");
        html.AppendLine("                                <th>Max</th>");
        html.AppendLine("                                <th>Validation Pass Rate</th>");
        html.AppendLine("                            </tr>");
        html.AppendLine("                        </thead>");
        html.AppendLine("                        <tbody>");

        foreach (var apiGroup in apiGroups.OrderBy(g => g.Key))
        {
            var apiResults = apiGroup.ToList();
            var totalRequests = apiResults.Count;
            var successfulRequests = apiResults.Count(r => r.IsSuccess);
            var successRate = (double)successfulRequests / totalRequests * 100;
            
            var responseTimes = apiResults.Select(r => r.ResponseTime.TotalMilliseconds).ToList();
            var avgResponseTime = responseTimes.Average();
            var minResponseTime = responseTimes.Min();
            var maxResponseTime = responseTimes.Max();
            
            // Calculate percentiles
            var sortedTimes = responseTimes.OrderBy(x => x).ToList();
            var p50 = GetPercentile(sortedTimes, 50);
            var p95 = GetPercentile(sortedTimes, 95);
            var p99 = GetPercentile(sortedTimes, 99);
            
            // Validation pass rate
            var validationResults = apiResults.Where(r => r.ValidationResult != null);
            var validationPassRate = validationResults.Any() 
                ? (double)validationResults.Count(r => r.ValidationResult!.IsSuccess) / validationResults.Count() * 100
                : 100.0;

            var successRateClass = successRate >= 95 ? "success" : successRate >= 90 ? "warning" : "error";
            var validationClass = validationPassRate >= 95 ? "success" : validationPassRate >= 90 ? "warning" : "error";

            html.AppendLine("                            <tr>");
            html.AppendLine($"                                <td class=\"endpoint-name\">{apiGroup.Key}</td>");
            html.AppendLine($"                                <td>{totalRequests:N0}</td>");
            html.AppendLine($"                                <td class=\"{successRateClass}\">{successRate:F1}%</td>");
            html.AppendLine($"                                <td>{avgResponseTime:F1} ms</td>");
            html.AppendLine($"                                <td>{p50:F1} ms</td>");
            html.AppendLine($"                                <td>{p95:F1} ms</td>");
            html.AppendLine($"                                <td>{p99:F1} ms</td>");
            html.AppendLine($"                                <td>{minResponseTime:F1} ms</td>");
            html.AppendLine($"                                <td>{maxResponseTime:F1} ms</td>");
            html.AppendLine($"                                <td class=\"{validationClass}\">{validationPassRate:F1}%</td>");
            html.AppendLine("                            </tr>");
        }

        html.AppendLine("                        </tbody>");
        html.AppendLine("                    </table>");
        html.AppendLine("                </div>");

        // Individual API details
        html.AppendLine("                <div class=\"api-details\">");
        html.AppendLine("                    <h3>Detailed API Analysis</h3>");
        
        foreach (var apiGroup in apiGroups.OrderBy(g => g.Key))
        {
            var apiResults = apiGroup.ToList();
            var errorResults = apiResults.Where(r => !r.IsSuccess).ToList();
            var validationFailures = apiResults.Where(r => r.ValidationResult != null && !r.ValidationResult.IsSuccess).ToList();

            html.AppendLine($"                    <div class=\"api-detail-card\">");
            html.AppendLine($"                        <h4>{apiGroup.Key}</h4>");
            html.AppendLine($"                        <div class=\"api-stats\">");
            html.AppendLine($"                            <div class=\"stat-item\">");
            html.AppendLine($"                                <span class=\"stat-label\">Total Requests:</span>");
            html.AppendLine($"                                <span class=\"stat-value\">{apiResults.Count:N0}</span>");
            html.AppendLine($"                            </div>");
            
            if (errorResults.Any())
            {
                html.AppendLine($"                            <div class=\"stat-item error\">");
                html.AppendLine($"                                <span class=\"stat-label\">Errors:</span>");
                html.AppendLine($"                                <span class=\"stat-value\">{errorResults.Count} ({(double)errorResults.Count / apiResults.Count * 100:F1}%)</span>");
                html.AppendLine($"                            </div>");
                
                // Show error details
                var errorGroups = errorResults.GroupBy(r => $"{r.StatusCode} - {r.ErrorMessage}").Take(5);
                foreach (var errorGroup in errorGroups)
                {
                    html.AppendLine($"                                <div class=\"error-detail\">");
                    html.AppendLine($"                                    {errorGroup.Key}: {errorGroup.Count()} occurrences");
                    html.AppendLine($"                                </div>");
                }
            }
            
            if (validationFailures.Any())
            {
                html.AppendLine($"                            <div class=\"stat-item warning\">");
                html.AppendLine($"                                <span class=\"stat-label\">Validation Failures:</span>");
                html.AppendLine($"                                <span class=\"stat-value\">{validationFailures.Count} ({(double)validationFailures.Count / apiResults.Count * 100:F1}%)</span>");
                html.AppendLine($"                            </div>");
            }
            
            html.AppendLine($"                        </div>");
            html.AppendLine($"                    </div>");
        }
        
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");

        return html.ToString();
    }

    private double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (!sortedValues.Any()) return 0;
        
        var index = (int)Math.Ceiling(sortedValues.Count * percentile / 100) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    private void OpenReportInBrowser(string reportPath)
    {
        try
        {
            var absolutePath = Path.GetFullPath(reportPath);
            var fileUrl = $"file://{absolutePath.Replace('\\', '/')}";
            
            _logger.LogInformation("Opening HTML report in default browser: {FileUrl}", fileUrl);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileUrl,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = fileUrl,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = fileUrl,
                    UseShellExecute = true
                });
            }
            else
            {
                _logger.LogWarning("Unable to determine OS platform for opening browser. Report available at: {ReportPath}", absolutePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open report in browser automatically. Report available at: {ReportPath}", reportPath);
        }
    }
}
