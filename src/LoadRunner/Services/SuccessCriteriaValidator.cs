using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoadRunner.Services;

public interface ISuccessCriteriaValidator
{
    Task<ValidationResult> ValidateResponseAsync(HttpResponseMessage response, string responseBody, SuccessCriteria? criteria, TimeSpan responseTime);
    ValidationResult ValidateWithGlobalCriteria(HttpResponseMessage response, string responseBody, TimeSpan responseTime);
}

public class SuccessCriteriaValidator : ISuccessCriteriaValidator
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<SuccessCriteriaValidator> _logger;

    public SuccessCriteriaValidator(IConfigurationManager configurationManager, ILogger<SuccessCriteriaValidator> logger)
    {
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateResponseAsync(HttpResponseMessage response, string responseBody, SuccessCriteria? criteria, TimeSpan responseTime)
    {
        var startTime = DateTime.UtcNow;
        var result = new ValidationResult { IsSuccess = true };

        try
        {
            if (criteria == null)
            {
                return ValidateWithGlobalCriteria(response, responseBody, responseTime);
            }

            // Validate HTTP Status Code
            if (criteria.HttpStatusCodes != null && criteria.HttpStatusCodes.Length > 0)
            {
                if (!criteria.HttpStatusCodes.Contains((int)response.StatusCode))
                {
                    result.IsSuccess = false;
                    result.FailureReasons.Add($"HTTP status code {(int)response.StatusCode} not in allowed list: [{string.Join(", ", criteria.HttpStatusCodes)}]");
                }
                result.ValidationDetails["HttpStatusValidation"] = $"Expected: [{string.Join(", ", criteria.HttpStatusCodes)}], Actual: {(int)response.StatusCode}";
            }

            // Validate Response Time
            if (criteria.ResponseTimeMaxMs.HasValue)
            {
                var responseTimeMs = responseTime.TotalMilliseconds;
                if (responseTimeMs > criteria.ResponseTimeMaxMs.Value)
                {
                    result.IsSuccess = false;
                    result.FailureReasons.Add($"Response time {responseTimeMs:F0}ms exceeded maximum allowed {criteria.ResponseTimeMaxMs.Value}ms");
                }
                result.ValidationDetails["ResponseTimeValidation"] = $"Expected: <={criteria.ResponseTimeMaxMs.Value}ms, Actual: {responseTimeMs:F0}ms";
            }

            // Validate Response Body Regex
            if (!string.IsNullOrWhiteSpace(criteria.ResponseBodyRegex))
            {
                try
                {
                    var regex = new Regex(criteria.ResponseBodyRegex, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (!regex.IsMatch(responseBody))
                    {
                        result.IsSuccess = false;
                        result.FailureReasons.Add($"Response body does not match regex pattern: {criteria.ResponseBodyRegex}");
                    }
                    result.ValidationDetails["RegexValidation"] = $"Pattern: {criteria.ResponseBodyRegex}, Match: {regex.IsMatch(responseBody)}";
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.FailureReasons.Add($"Invalid regex pattern: {criteria.ResponseBodyRegex} - {ex.Message}");
                }
            }

            // Validate Response Body Contains
            if (criteria.ResponseBodyContains != null && criteria.ResponseBodyContains.Length > 0)
            {
                var missingStrings = new List<string>();
                foreach (var requiredString in criteria.ResponseBodyContains)
                {
                    if (!responseBody.Contains(requiredString, StringComparison.OrdinalIgnoreCase))
                    {
                        missingStrings.Add(requiredString);
                    }
                }

                if (missingStrings.Count > 0)
                {
                    result.IsSuccess = false;
                    result.FailureReasons.Add($"Response body missing required strings: [{string.Join(", ", missingStrings)}]");
                }
                result.ValidationDetails["ContainsValidation"] = $"Required: [{string.Join(", ", criteria.ResponseBodyContains)}], Missing: [{string.Join(", ", missingStrings)}]";
            }

            // Validate Response Headers
            if (criteria.ResponseHeaderChecks != null)
            {
                foreach (var headerCheck in criteria.ResponseHeaderChecks)
                {
                    var headerValidation = ValidateHeader(response, headerCheck);
                    if (!headerValidation.IsValid)
                    {
                        result.IsSuccess = false;
                        result.FailureReasons.Add(headerValidation.ErrorMessage);
                    }
                    result.ValidationDetails[$"Header_{headerCheck.HeaderName}"] = headerValidation.Details;
                }
            }

            // Validate JSON Path
            if (criteria.JsonPathValidations != null)
            {
                foreach (var jsonValidation in criteria.JsonPathValidations)
                {
                    var jsonValidationResult = await ValidateJsonPathAsync(responseBody, jsonValidation);
                    if (!jsonValidationResult.IsValid)
                    {
                        result.IsSuccess = false;
                        result.FailureReasons.Add(jsonValidationResult.ErrorMessage);
                    }
                    result.ValidationDetails[$"JsonPath_{jsonValidation.JsonPath}"] = jsonValidationResult.Details;
                }
            }

            // Validate Response Size
            if (criteria.ResponseSizeMinBytes.HasValue || criteria.ResponseSizeMaxBytes.HasValue)
            {
                var responseSize = responseBody.Length;
                
                if (criteria.ResponseSizeMinBytes.HasValue && responseSize < criteria.ResponseSizeMinBytes.Value)
                {
                    result.IsSuccess = false;
                    result.FailureReasons.Add($"Response size {responseSize} bytes is below minimum {criteria.ResponseSizeMinBytes.Value} bytes");
                }
                
                if (criteria.ResponseSizeMaxBytes.HasValue && responseSize > criteria.ResponseSizeMaxBytes.Value)
                {
                    result.IsSuccess = false;
                    result.FailureReasons.Add($"Response size {responseSize} bytes exceeds maximum {criteria.ResponseSizeMaxBytes.Value} bytes");
                }
                
                result.ValidationDetails["ResponseSizeValidation"] = $"Size: {responseSize} bytes, Min: {criteria.ResponseSizeMinBytes}, Max: {criteria.ResponseSizeMaxBytes}";
            }

            result.ValidationDuration = DateTime.UtcNow - startTime;
            
            if (result.IsSuccess)
            {
                _logger.LogDebug("All validation criteria passed for response");
            }
            else
            {
                _logger.LogWarning("Validation failed: {Reasons}", string.Join("; ", result.FailureReasons));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during response validation");
            result.IsSuccess = false;
            result.FailureReasons.Add($"Validation error: {ex.Message}");
            result.ValidationDuration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    public ValidationResult ValidateWithGlobalCriteria(HttpResponseMessage response, string responseBody, TimeSpan responseTime)
    {
        var globalCriteria = _configurationManager.Configuration.GlobalSuccessCriteria;
        var criteria = new SuccessCriteria
        {
            HttpStatusCodes = globalCriteria.DefaultHttpStatusCodes,
            ResponseTimeMaxMs = globalCriteria.DefaultResponseTimeMaxMs
        };

        return ValidateResponseAsync(response, responseBody, criteria, responseTime).Result;
    }

    private (bool IsValid, string ErrorMessage, string Details) ValidateHeader(HttpResponseMessage response, ResponseHeaderCheck headerCheck)
    {
        var headerExists = response.Headers.TryGetValues(headerCheck.HeaderName, out var headerValues) ||
                          response.Content.Headers.TryGetValues(headerCheck.HeaderName, out headerValues);

        var headerValue = headerValues?.FirstOrDefault();

        return headerCheck.ValidationRule.ToLowerInvariant() switch
        {
            "notnull" => (headerExists, 
                         headerExists ? "" : $"Header '{headerCheck.HeaderName}' is missing",
                         $"Exists: {headerExists}"),
            
            "equals" => (headerExists && headerValue == headerCheck.ExpectedValue,
                        headerExists ? 
                            (headerValue == headerCheck.ExpectedValue ? "" : $"Header '{headerCheck.HeaderName}' value '{headerValue}' does not equal expected '{headerCheck.ExpectedValue}'") :
                            $"Header '{headerCheck.HeaderName}' is missing",
                        $"Expected: {headerCheck.ExpectedValue}, Actual: {headerValue}"),
            
            "contains" => (headerExists && (headerValue?.Contains(headerCheck.ExpectedValue ?? "", StringComparison.OrdinalIgnoreCase) ?? false),
                          headerExists ?
                              ((headerValue?.Contains(headerCheck.ExpectedValue ?? "", StringComparison.OrdinalIgnoreCase) ?? false) ? "" : $"Header '{headerCheck.HeaderName}' value '{headerValue}' does not contain '{headerCheck.ExpectedValue}'") :
                              $"Header '{headerCheck.HeaderName}' is missing",
                          $"Expected to contain: {headerCheck.ExpectedValue}, Actual: {headerValue}"),
            
            "regex" => ValidateHeaderRegex(headerCheck.HeaderName, headerValue, headerCheck.ExpectedValue ?? ""),
            
            _ => (false, $"Unknown validation rule: {headerCheck.ValidationRule}", $"Rule: {headerCheck.ValidationRule}")
        };
    }

    private (bool IsValid, string ErrorMessage, string Details) ValidateHeaderRegex(string headerName, string? headerValue, string pattern)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return (false, $"Header '{headerName}' is missing or empty", $"Pattern: {pattern}, Value: null");
            }

            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var isMatch = regex.IsMatch(headerValue);
            
            return (isMatch,
                   isMatch ? "" : $"Header '{headerName}' value '{headerValue}' does not match pattern '{pattern}'",
                   $"Pattern: {pattern}, Value: {headerValue}, Match: {isMatch}");
        }
        catch (Exception ex)
        {
            return (false, $"Invalid regex pattern for header '{headerName}': {ex.Message}", $"Pattern: {pattern}");
        }
    }

    private async Task<(bool IsValid, string ErrorMessage, string Details)> ValidateJsonPathAsync(string responseBody, JsonPathValidation validation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return (false, $"Response body is empty, cannot validate JSON path: {validation.JsonPath}", "Empty response");
            }

            using var document = JsonDocument.Parse(responseBody);
            var pathParts = validation.JsonPath.TrimStart('$').Trim('.').Split('.');
            
            JsonElement? currentElement = document.RootElement;
            
            foreach (var part in pathParts.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                if (currentElement?.TryGetProperty(part, out var nextElement) == true)
                {
                    currentElement = nextElement;
                }
                else
                {
                    return (false, $"JSON path '{validation.JsonPath}' not found in response", $"Path: {validation.JsonPath}");
                }
            }

            if (!currentElement.HasValue)
            {
                return (false, $"JSON path '{validation.JsonPath}' resulted in null", $"Path: {validation.JsonPath}");
            }

            var element = currentElement.Value;

            return validation.ValidationRule.ToLowerInvariant() switch
            {
                "notnull" => (element.ValueKind != JsonValueKind.Null,
                             element.ValueKind != JsonValueKind.Null ? "" : $"JSON path '{validation.JsonPath}' is null",
                             $"Path: {validation.JsonPath}, ValueKind: {element.ValueKind}"),

                "isnumeric" => (element.ValueKind == JsonValueKind.Number,
                               element.ValueKind == JsonValueKind.Number ? "" : $"JSON path '{validation.JsonPath}' is not numeric",
                               $"Path: {validation.JsonPath}, ValueKind: {element.ValueKind}"),

                "isstring" => (element.ValueKind == JsonValueKind.String,
                              element.ValueKind == JsonValueKind.String ? "" : $"JSON path '{validation.JsonPath}' is not a string",
                              $"Path: {validation.JsonPath}, ValueKind: {element.ValueKind}"),

                "equals" => ValidateJsonEquals(element, validation),

                "regex" => ValidateJsonRegex(element, validation),

                _ => (false, $"Unknown JSON validation rule: {validation.ValidationRule}", $"Rule: {validation.ValidationRule}")
            };
        }
        catch (JsonException ex)
        {
            return (false, $"Invalid JSON in response: {ex.Message}", $"JSON Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"JSON path validation error: {ex.Message}", $"Error: {ex.Message}");
        }
    }

    private (bool IsValid, string ErrorMessage, string Details) ValidateJsonEquals(JsonElement element, JsonPathValidation validation)
    {
        try
        {
            var actualValue = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDecimal().ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => element.GetRawText()
            };

            var expectedValue = validation.ExpectedValue ?? "";
            var isEqual = string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);

            return (isEqual,
                   isEqual ? "" : $"JSON path '{validation.JsonPath}' value '{actualValue}' does not equal expected '{expectedValue}'",
                   $"Expected: {expectedValue}, Actual: {actualValue}");
        }
        catch (Exception ex)
        {
            return (false, $"Error comparing JSON values: {ex.Message}", $"Error: {ex.Message}");
        }
    }

    private (bool IsValid, string ErrorMessage, string Details) ValidateJsonRegex(JsonElement element, JsonPathValidation validation)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                return (false, $"JSON path '{validation.JsonPath}' must be a string for regex validation", $"ValueKind: {element.ValueKind}");
            }

            var value = element.GetString() ?? "";
            var pattern = validation.ExpectedValue ?? "";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var isMatch = regex.IsMatch(value);

            return (isMatch,
                   isMatch ? "" : $"JSON path '{validation.JsonPath}' value '{value}' does not match pattern '{pattern}'",
                   $"Pattern: {pattern}, Value: {value}, Match: {isMatch}");
        }
        catch (Exception ex)
        {
            return (false, $"JSON regex validation error: {ex.Message}", $"Error: {ex.Message}");
        }
    }
}
