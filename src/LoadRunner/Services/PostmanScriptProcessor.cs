using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoadRunner.Services;

public interface IPostmanScriptProcessor
{
    Dictionary<string, string> ExecuteTestScript(PostmanItem item, TestExecutionResult result, string responseBody, Dictionary<string, string> currentVariables);
    Dictionary<string, string> ExecutePreRequestScript(PostmanItem item, Dictionary<string, string> currentVariables);
}

public class PostmanScriptProcessor : IPostmanScriptProcessor
{
    private readonly ILogger<PostmanScriptProcessor> _logger;

    public PostmanScriptProcessor(ILogger<PostmanScriptProcessor> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, string> ExecuteTestScript(PostmanItem item, TestExecutionResult result, string responseBody, Dictionary<string, string> currentVariables)
    {
        var updatedVariables = new Dictionary<string, string>(currentVariables);
        
        if (item.Events == null) return updatedVariables;

        var testEvent = item.Events.FirstOrDefault(e => e.Listen == "test");
        if (testEvent?.Script?.Exec == null) return updatedVariables;

        _logger.LogDebug("Executing test script for request: {RequestName}", item.Name);

        try
        {
            var scriptLines = testEvent.Script.Exec;
            var scriptContext = new ScriptExecutionContext
            {
                ResponseBody = responseBody,
                RequestBody = GetRequestBody(item),
                Variables = updatedVariables,
                StatusCode = result.StatusCode
            };

            foreach (var line in scriptLines)
            {
                ExecuteScriptLine(line, scriptContext);
            }

            updatedVariables = scriptContext.Variables;
            
            _logger.LogDebug("Test script executed successfully. Variables updated: {Count}", 
                updatedVariables.Count - currentVariables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute test script for {RequestName}", item.Name);
        }

        return updatedVariables;
    }

    public Dictionary<string, string> ExecutePreRequestScript(PostmanItem item, Dictionary<string, string> currentVariables)
    {
        var updatedVariables = new Dictionary<string, string>(currentVariables);
        
        if (item.Events == null) return updatedVariables;

        var preRequestEvent = item.Events.FirstOrDefault(e => e.Listen == "prerequest");
        if (preRequestEvent?.Script?.Exec == null) return updatedVariables;

        _logger.LogDebug("Executing pre-request script for request: {RequestName}", item.Name);

        try
        {
            var scriptLines = preRequestEvent.Script.Exec;
            var scriptContext = new ScriptExecutionContext
            {
                Variables = updatedVariables
            };

            foreach (var line in scriptLines)
            {
                ExecuteScriptLine(line, scriptContext);
            }

            updatedVariables = scriptContext.Variables;
            
            _logger.LogDebug("Pre-request script executed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute pre-request script for {RequestName}", item.Name);
        }

        return updatedVariables;
    }

    private void ExecuteScriptLine(string line, ScriptExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("//")) 
            return;

        var cleanLine = line.Trim().TrimEnd(';');

        try
        {
            // Handle JSON.parse(responseBody).property
            if (cleanLine.Contains("JSON.parse(responseBody)"))
            {
                HandleJsonParseResponseBody(cleanLine, context);
            }
            // Handle JSON.parse(pm.request.body.raw)
            else if (cleanLine.Contains("JSON.parse(pm.request.body.raw)"))
            {
                HandleJsonParseRequestBody(cleanLine, context);
            }
            // Handle JSON.stringify()
            else if (cleanLine.Contains("JSON.stringify"))
            {
                HandleJsonStringify(cleanLine, context);
            }
            // Handle btoa() - Base64 encoding
            else if (cleanLine.Contains("btoa("))
            {
                HandleBase64Encode(cleanLine, context);
            }
            // Handle pm.collectionVariables.set()
            else if (cleanLine.Contains("pm.collectionVariables.set"))
            {
                HandleSetCollectionVariable(cleanLine, context);
            }
            // Handle variable assignments
            else if (cleanLine.Contains("var ") && cleanLine.Contains("="))
            {
                HandleVariableAssignment(cleanLine, context);
            }

            _logger.LogDebug("Executed script line: {Line}", cleanLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to execute script line: {Line}", cleanLine);
        }
    }

    private void HandleJsonParseResponseBody(string line, ScriptExecutionContext context)
    {
        // Extract property path from JSON.parse(responseBody).property
        var match = Regex.Match(line, @"JSON\.parse\(responseBody\)\.(\w+)");
        if (!match.Success) return;

        var propertyName = match.Groups[1].Value;
        
        try
        {
            using var document = JsonDocument.Parse(context.ResponseBody ?? "{}");
            if (document.RootElement.TryGetProperty(propertyName, out var property))
            {
                var value = property.ValueKind == JsonValueKind.String 
                    ? property.GetString() ?? ""
                    : property.GetRawText();

                // Extract variable name if this is an assignment
                var varMatch = Regex.Match(line, @"var\s+(\w+)\s*=");
                if (varMatch.Success)
                {
                    var variableName = varMatch.Groups[1].Value;
                    context.TempVariables[variableName] = value;
                    _logger.LogDebug("Extracted {Property} = {Value} into temp variable {Variable}", 
                        propertyName, value, variableName);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse response body as JSON");
        }
    }

    private void HandleJsonParseRequestBody(string line, ScriptExecutionContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(context.RequestBody)) return;

            using var document = JsonDocument.Parse(context.RequestBody);
            
            var varMatch = Regex.Match(line, @"var\s+(\w+)\s*=");
            if (varMatch.Success)
            {
                var variableName = varMatch.Groups[1].Value;
                context.TempVariables[variableName] = document.RootElement.ToString();
                _logger.LogDebug("Parsed request body into temp variable {Variable}", variableName);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse request body as JSON");
        }
    }

    private void HandleJsonStringify(string line, ScriptExecutionContext context)
    {
        var match = Regex.Match(line, @"var\s+(\w+)\s*=\s*JSON\.stringify\((\w+)\)");
        if (!match.Success) return;

        var resultVar = match.Groups[1].Value;
        var sourceVar = match.Groups[2].Value;

        if (context.TempVariables.TryGetValue(sourceVar, out var sourceValue))
        {
            context.TempVariables[resultVar] = sourceValue; // Already JSON string
            _logger.LogDebug("Stringified {Source} into {Result}", sourceVar, resultVar);
        }
    }

    private void HandleBase64Encode(string line, ScriptExecutionContext context)
    {
        var match = Regex.Match(line, @"var\s+(\w+)\s*=\s*btoa\((\w+)\)");
        if (!match.Success) return;

        var resultVar = match.Groups[1].Value;
        var sourceVar = match.Groups[2].Value;

        if (context.TempVariables.TryGetValue(sourceVar, out var sourceValue))
        {
            var base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceValue));
            context.TempVariables[resultVar] = base64Value;
            _logger.LogDebug("Base64 encoded {Source} into {Result}: {Value}", 
                sourceVar, resultVar, base64Value);
        }
    }

    private void HandleSetCollectionVariable(string line, ScriptExecutionContext context)
    {
        var match = Regex.Match(line, @"pm\.collectionVariables\.set\([""'](\w+)[""'],\s*(\w+)\)");
        if (!match.Success) return;

        var variableName = match.Groups[1].Value;
        var sourceVar = match.Groups[2].Value;

        if (context.TempVariables.TryGetValue(sourceVar, out var value))
        {
            context.Variables[variableName] = value;
            _logger.LogDebug("Set collection variable {Variable} = {Value}", variableName, value);
        }
    }

    private void HandleVariableAssignment(string line, ScriptExecutionContext context)
    {
        var match = Regex.Match(line, @"var\s+(\w+)\s*=\s*(.+)");
        if (!match.Success) return;

        var variableName = match.Groups[1].Value;
        var expression = match.Groups[2].Value.Trim();

        // Handle simple string literals
        if (expression.StartsWith('"') && expression.EndsWith('"'))
        {
            var value = expression.Trim('"');
            context.TempVariables[variableName] = value;
            _logger.LogDebug("Assigned string literal to {Variable}: {Value}", variableName, value);
        }
    }

    private string GetRequestBody(PostmanItem item)
    {
        return item.Request.Body?.Raw ?? "";
    }

    private class ScriptExecutionContext
    {
        public string ResponseBody { get; set; } = "";
        public string RequestBody { get; set; } = "";
        public Dictionary<string, string> Variables { get; set; } = new();
        public Dictionary<string, string> TempVariables { get; set; } = new();
        public int StatusCode { get; set; }
    }
}
