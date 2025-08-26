using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace LoadRunner.Services;

public interface IHttpRequestBuilder
{
    HttpRequestMessage BuildRequest(PostmanItem item, Dictionary<string, string> variables);
    string ReplaceVariables(string template, Dictionary<string, string> variables);
}

public class HttpRequestBuilder : IHttpRequestBuilder
{
    private readonly ILogger<HttpRequestBuilder> _logger;

    public HttpRequestBuilder(ILogger<HttpRequestBuilder> logger)
    {
        _logger = logger;
    }

    public HttpRequestMessage BuildRequest(PostmanItem item, Dictionary<string, string> variables)
    {
        try
        {
            var request = item.Request;
            var method = new HttpMethod(request.Method.ToUpperInvariant());
            
            // Build URL
            var url = BuildUrl(request.Url, variables);
            var httpRequest = new HttpRequestMessage(method, url);

            // Add headers
            AddHeaders(httpRequest, request.Headers, variables);

            // Add body if present
            if (request.Body != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
            {
                AddBody(httpRequest, request.Body, variables);
            }

            _logger.LogDebug("Built HTTP request: {Method} {Url}", method, url);
            return httpRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build HTTP request for {RequestName}", item.Name);
            throw;
        }
    }

    public string ReplaceVariables(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        var result = template;
        
        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            result = result.Replace(placeholder, variable.Value, StringComparison.OrdinalIgnoreCase);
        }

        _logger.LogDebug("Variable replacement: {Original} -> {Result}", template, result);
        return result;
    }

    private string BuildUrl(PostmanUrl postmanUrl, Dictionary<string, string> variables)
    {
        // Use raw URL if available and properly formatted
        if (!string.IsNullOrWhiteSpace(postmanUrl.Raw))
        {
            var url = ReplaceVariables(postmanUrl.Raw, variables);
            
            // Ensure URL is properly formatted
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }
        }

        // Build URL from components
        var baseUrl = string.Empty;
        
        // Build host
        if (postmanUrl.Host.Count > 0)
        {
            var hostParts = postmanUrl.Host.Select(h => ReplaceVariables(h, variables)).ToList();
            baseUrl = string.Join(".", hostParts);
            
            // Add protocol if not present
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
            {
                baseUrl = "https://" + baseUrl;
            }
        }

        // Add path
        if (postmanUrl.Path.Count > 0)
        {
            var pathParts = postmanUrl.Path.Select(p => ReplaceVariables(p, variables)).Where(p => !string.IsNullOrWhiteSpace(p));
            var path = string.Join("/", pathParts);
            baseUrl = baseUrl.TrimEnd('/') + "/" + path;
        }

        // Add query parameters
        if (postmanUrl.Query != null && postmanUrl.Query.Count > 0)
        {
            var queryParams = new List<string>();
            
            foreach (var queryParam in postmanUrl.Query.Where(q => !q.Disabled))
            {
                var key = ReplaceVariables(queryParam.Key, variables);
                var value = ReplaceVariables(queryParam.Value, variables);
                queryParams.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }

            if (queryParams.Count > 0)
            {
                baseUrl += "?" + string.Join("&", queryParams);
            }
        }

        return baseUrl;
    }

    private void AddHeaders(HttpRequestMessage request, List<PostmanHeader> headers, Dictionary<string, string> variables)
    {
        foreach (var header in headers.Where(h => !h.Disabled))
        {
            var name = ReplaceVariables(header.Key, variables);
            var value = ReplaceVariables(header.Value, variables);

            try
            {
                // Try to add as request header first
                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    // If that fails, try as content header (will be set when content is added)
                    _logger.LogDebug("Deferred header for content: {Name} = {Value}", name, value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add header {Name} = {Value}", name, value);
            }
        }
    }

    private void AddBody(HttpRequestMessage request, PostmanBody body, Dictionary<string, string> variables)
    {
        switch (body.Mode.ToLowerInvariant())
        {
            case "raw":
                AddRawBody(request, body, variables);
                break;
            case "urlencoded":
                AddUrlEncodedBody(request, body, variables);
                break;
            case "formdata":
                AddFormDataBody(request, body, variables);
                break;
            default:
                _logger.LogWarning("Unsupported body mode: {Mode}", body.Mode);
                break;
        }
    }

    private void AddRawBody(HttpRequestMessage request, PostmanBody body, Dictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(body.Raw))
            return;

        var content = ReplaceVariables(body.Raw, variables);
        
        // Try to detect content type from the content
        var contentType = "text/plain";
        
        try
        {
            // Check if it's JSON
            if (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
            {
                JsonDocument.Parse(content); // Validate JSON
                contentType = "application/json";
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, keep as text/plain
        }

        request.Content = new StringContent(content, Encoding.UTF8, contentType);
        _logger.LogDebug("Added raw body with content type: {ContentType}", contentType);
    }

    private void AddUrlEncodedBody(HttpRequestMessage request, PostmanBody body, Dictionary<string, string> variables)
    {
        if (body.UrlEncoded == null || body.UrlEncoded.Count == 0)
            return;

        var formData = new List<KeyValuePair<string, string>>();
        
        foreach (var param in body.UrlEncoded)
        {
            var key = ReplaceVariables(param.Key, variables);
            var value = ReplaceVariables(param.Value, variables);
            formData.Add(new KeyValuePair<string, string>(key, value));
        }

        request.Content = new FormUrlEncodedContent(formData);
        _logger.LogDebug("Added URL-encoded body with {Count} parameters", formData.Count);
    }

    private void AddFormDataBody(HttpRequestMessage request, PostmanBody body, Dictionary<string, string> variables)
    {
        if (body.FormData == null || body.FormData.Count == 0)
            return;

        var formData = new MultipartFormDataContent();
        
        foreach (var param in body.FormData)
        {
            var key = ReplaceVariables(param.Key, variables);
            var value = ReplaceVariables(param.Value, variables);
            formData.Add(new StringContent(value), key);
        }

        request.Content = formData;
        _logger.LogDebug("Added form data body with {Count} parameters", body.FormData.Count);
    }
}
