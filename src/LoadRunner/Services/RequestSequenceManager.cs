using LoadRunner.Models;
using Microsoft.Extensions.Logging;

namespace LoadRunner.Services;

public interface IRequestSequenceManager
{
    Task<Dictionary<string, string>> ExecuteAuthenticationFlowAsync(Dictionary<string, string> initialVariables);
    Task<TestExecutionResult> ExecuteRequestWithDependenciesAsync(string requestName, Dictionary<string, string> variables, SuccessCriteria? successCriteria = null);
}

public class RequestSequenceManager : IRequestSequenceManager
{
    private readonly IHttpClientManager _httpClientManager;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IPostmanScriptProcessor _scriptProcessor;
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<RequestSequenceManager> _logger;
    private PostmanCollection? _postmanCollection;

    public RequestSequenceManager(
        IHttpClientManager httpClientManager,
        IHttpRequestBuilder requestBuilder,
        IPostmanScriptProcessor scriptProcessor,
        IConfigurationManager configurationManager,
        ILogger<RequestSequenceManager> logger)
    {
        _httpClientManager = httpClientManager;
        _requestBuilder = requestBuilder;
        _scriptProcessor = scriptProcessor;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> ExecuteAuthenticationFlowAsync(Dictionary<string, string> initialVariables)
    {
        _postmanCollection ??= _configurationManager.LoadPostmanCollection();
        var currentVariables = new Dictionary<string, string>(initialVariables);

        _logger.LogInformation("Starting authentication flow");

        try
        {
            // Step 1: Get JWT token
            var jwtResult = await ExecuteAuthenticationStepAsync("jwt-server-si/token", currentVariables);
            if (!jwtResult.success)
            {
                _logger.LogError("JWT token request failed");
                return currentVariables;
            }
            currentVariables = jwtResult.variables;

            _logger.LogDebug("JWT token obtained. Variables after Step 1: {Count}", currentVariables.Count);
            LogVariableState(currentVariables, "After JWT request");

            // Step 2: Get access token using JWT
            var accessTokenResult = await ExecuteAuthenticationStepAsync("token password auto", currentVariables);
            if (!accessTokenResult.success)
            {
                _logger.LogError("Access token request failed");
                return currentVariables;
            }
            currentVariables = accessTokenResult.variables;

            _logger.LogInformation("Authentication flow completed successfully");
            LogVariableState(currentVariables, "After access token request");

            return currentVariables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication flow failed");
            return initialVariables;
        }
    }

    public async Task<TestExecutionResult> ExecuteRequestWithDependenciesAsync(string requestName, Dictionary<string, string> variables, SuccessCriteria? successCriteria = null)
    {
        _postmanCollection ??= _configurationManager.LoadPostmanCollection();
        
        var postmanItem = _postmanCollection.Items.FirstOrDefault(i => 
            i.Name.Equals(requestName, StringComparison.OrdinalIgnoreCase));
        
        if (postmanItem == null)
        {
            _logger.LogWarning("Request not found: {RequestName}", requestName);
            return new TestExecutionResult
            {
                RequestName = requestName,
                IsSuccess = false,
                ErrorMessage = "Request not found in collection",
                Timestamp = DateTime.UtcNow
            };
        }

        // Check if this request needs authentication tokens
        var needsAuth = RequiresAuthentication(postmanItem);
        var currentVariables = new Dictionary<string, string>(variables);

        if (needsAuth && !HasValidTokens(currentVariables))
        {
            _logger.LogDebug("Request {RequestName} requires authentication. Running auth flow first.", requestName);
            currentVariables = await ExecuteAuthenticationFlowAsync(currentVariables);
        }

        // Execute the actual request
        return await ExecuteRequestWithScriptsAsync(postmanItem, currentVariables, successCriteria);
    }

    private async Task<(bool success, Dictionary<string, string> variables)> ExecuteAuthenticationStepAsync(string stepName, Dictionary<string, string> variables)
    {
        _postmanCollection ??= _configurationManager.LoadPostmanCollection();
        
        var postmanItem = _postmanCollection.Items.FirstOrDefault(i => 
            i.Name.Equals(stepName, StringComparison.OrdinalIgnoreCase));

        if (postmanItem == null)
        {
            _logger.LogError("Authentication step not found: {StepName}", stepName);
            return (false, variables);
        }

        var result = await ExecuteRequestWithScriptsAsync(postmanItem, variables, null);
        return (result.IsSuccess, variables);
    }

    private async Task<TestExecutionResult> ExecuteRequestWithScriptsAsync(PostmanItem item, Dictionary<string, string> variables, SuccessCriteria? successCriteria)
    {
        _logger.LogDebug("Executing request with scripts: {RequestName}", item.Name);

        // Execute pre-request script
        var updatedVariables = _scriptProcessor.ExecutePreRequestScript(item, variables);
        LogVariableState(updatedVariables, $"After pre-request script for {item.Name}");

        // Build and execute HTTP request
        var httpRequest = _requestBuilder.BuildRequest(item, updatedVariables);
        var result = await _httpClientManager.ExecuteRequestAsync(httpRequest, item.Name, successCriteria);

        // Execute test script (post-request processing)
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.ResponseBody))
        {
            var finalVariables = _scriptProcessor.ExecuteTestScript(item, result, result.ResponseBody, updatedVariables);
            
            // Update the shared variables dictionary
            foreach (var kvp in finalVariables)
            {
                variables[kvp.Key] = kvp.Value;
            }
            
            LogVariableState(finalVariables, $"After test script for {item.Name}");
        }

        return result;
    }

    private bool RequiresAuthentication(PostmanItem item)
    {
        // Check if request uses access_token or other auth-related variables
        var requestJson = System.Text.Json.JsonSerializer.Serialize(item);
        return requestJson.Contains("{{access_token}}") || 
               requestJson.Contains("Bearer {{access_token}}") ||
               requestJson.Contains("Authorization");
    }

    private bool HasValidTokens(Dictionary<string, string> variables)
    {
        // Check if we have the required tokens
        return variables.ContainsKey("access_token") && 
               !string.IsNullOrWhiteSpace(variables["access_token"]) &&
               variables.ContainsKey("confirmation") && 
               !string.IsNullOrWhiteSpace(variables["confirmation"]);
    }

    private void LogVariableState(Dictionary<string, string> variables, string context)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;

        _logger.LogDebug("{Context} - Variables ({Count}):", context, variables.Count);
        foreach (var kvp in variables.OrderBy(x => x.Key))
        {
            var value = kvp.Value.Length > 50 ? kvp.Value.Substring(0, 47) + "..." : kvp.Value;
            _logger.LogDebug("  {Key} = {Value}", kvp.Key, value);
        }
    }
}
