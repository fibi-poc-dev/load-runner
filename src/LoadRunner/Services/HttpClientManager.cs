using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LoadRunner.Services;

public interface IHttpClientManager
{
    Task<TestExecutionResult> ExecuteRequestAsync(PostmanItem item, Dictionary<string, string> variables, SuccessCriteria? successCriteria = null);
    Task<TestExecutionResult> ExecuteRequestAsync(HttpRequestMessage request, string requestName, SuccessCriteria? successCriteria = null);
}

public class HttpClientManager : IHttpClientManager
{
    private readonly HttpClient _httpClient;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly ISuccessCriteriaValidator _validator;
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<HttpClientManager> _logger;

    public HttpClientManager(
        HttpClient httpClient,
        IHttpRequestBuilder requestBuilder,
        ISuccessCriteriaValidator validator,
        IConfigurationManager configurationManager,
        ILogger<HttpClientManager> logger)
    {
        _httpClient = httpClient;
        _requestBuilder = requestBuilder;
        _validator = validator;
        _configurationManager = configurationManager;
        _logger = logger;

        // Configure HttpClient
        var config = _configurationManager.Configuration;
        _httpClient.Timeout = TimeSpan.FromMilliseconds(config.PerformanceSettings.RequestTimeoutMs);
        
        // Configure SSL settings based on global criteria
        if (config.GlobalSuccessCriteria.IgnoreSslErrors)
        {
            // This would need to be configured at the HttpClientHandler level during DI setup
            _logger.LogWarning("SSL error ignoring is configured but requires HttpClientHandler setup");
        }
    }

    public async Task<TestExecutionResult> ExecuteRequestAsync(PostmanItem item, Dictionary<string, string> variables, SuccessCriteria? successCriteria = null)
    {
        var request = _requestBuilder.BuildRequest(item, variables);
        return await ExecuteRequestAsync(request, item.Name, successCriteria);
    }

    public async Task<TestExecutionResult> ExecuteRequestAsync(HttpRequestMessage request, string requestName, SuccessCriteria? successCriteria = null)
    {
        var result = new TestExecutionResult
        {
            RequestName = requestName,
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString() ?? "Unknown",
            Timestamp = DateTime.UtcNow
        };

        // Copy request headers for logging
        foreach (var header in request.Headers)
        {
            result.RequestHeaders[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content?.Headers != null)
        {
            foreach (var header in request.Content.Headers)
            {
                result.RequestHeaders[header.Key] = string.Join(", ", header.Value);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string responseBody = string.Empty;

        try
        {
            _logger.LogDebug("Executing request: {Method} {Url}", result.Method, result.Url);
            
            response = await _httpClient.SendAsync(request);
            stopwatch.Stop();
            
            result.ResponseTime = stopwatch.Elapsed;
            result.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
            {
                result.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            if (response.Content?.Headers != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    result.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
                }
            }

            // Read response body
            responseBody = await response.Content.ReadAsStringAsync();
            result.ResponseSizeBytes = responseBody.Length;
            result.ResponseBody = responseBody;

            // Validate success criteria
            if (successCriteria != null)
            {
                result.ValidationResult = await _validator.ValidateResponseAsync(response, responseBody, successCriteria, result.ResponseTime);
                result.IsSuccess = response.IsSuccessStatusCode && result.ValidationResult.IsSuccess;
            }
            else
            {
                // Use global success criteria
                result.ValidationResult = _validator.ValidateWithGlobalCriteria(response, responseBody, result.ResponseTime);
                result.IsSuccess = response.IsSuccessStatusCode && result.ValidationResult.IsSuccess;
            }

            if (result.IsSuccess)
            {
                _logger.LogDebug("Request completed successfully: {Method} {Url} - {StatusCode} in {ResponseTime}ms", 
                    result.Method, result.Url, result.StatusCode, result.ResponseTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Request failed validation: {Method} {Url} - {StatusCode} in {ResponseTime}ms. Reasons: {Reasons}",
                    result.Method, result.Url, result.StatusCode, result.ResponseTime.TotalMilliseconds, 
                    string.Join("; ", result.ValidationResult?.FailureReasons ?? new List<string>()));
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;
            result.IsSuccess = false;
            result.ErrorMessage = $"Request timeout after {result.ResponseTime.TotalMilliseconds:F0}ms";
            result.StatusCode = 408; // Request Timeout
            
            _logger.LogWarning("Request timeout: {Method} {Url} after {ResponseTime}ms", 
                result.Method, result.Url, result.ResponseTime.TotalMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;
            result.IsSuccess = false;
            result.ErrorMessage = $"HTTP request error: {ex.Message}";
            result.StatusCode = 0; // Network error
            
            _logger.LogError(ex, "HTTP request error: {Method} {Url}", result.Method, result.Url);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;
            result.IsSuccess = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            result.StatusCode = 0;
            
            _logger.LogError(ex, "Unexpected error during request execution: {Method} {Url}", result.Method, result.Url);
        }
        finally
        {
            response?.Dispose();
        }

        return result;
    }
}
