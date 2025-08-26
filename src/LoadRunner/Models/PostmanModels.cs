using System.Text.Json.Serialization;

namespace LoadRunner.Models;

public class PostmanCollection
{
    [JsonPropertyName("info")]
    public PostmanInfo Info { get; set; } = new();
    
    [JsonPropertyName("item")]
    public List<PostmanItem> Items { get; set; } = new();
    
    [JsonPropertyName("variable")]
    public List<PostmanVariable> Variables { get; set; } = new();
}

public class PostmanInfo
{
    [JsonPropertyName("_postman_id")]
    public string PostmanId { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;
}

public class PostmanItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("request")]
    public PostmanRequest Request { get; set; } = new();
    
    [JsonPropertyName("event")]
    public List<PostmanEvent>? Events { get; set; }
}

public class PostmanRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;
    
    [JsonPropertyName("header")]
    public List<PostmanHeader> Headers { get; set; } = new();
    
    [JsonPropertyName("body")]
    public PostmanBody? Body { get; set; }
    
    [JsonPropertyName("url")]
    public PostmanUrl Url { get; set; } = new();
}

public class PostmanHeader
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}

public class PostmanBody
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;
    
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }
    
    [JsonPropertyName("urlencoded")]
    public List<PostmanFormParameter>? UrlEncoded { get; set; }
    
    [JsonPropertyName("formdata")]
    public List<PostmanFormParameter>? FormData { get; set; }
}

public class PostmanFormParameter
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class PostmanUrl
{
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;
    
    [JsonPropertyName("host")]
    public List<string> Host { get; set; } = new();
    
    [JsonPropertyName("path")]
    public List<string> Path { get; set; } = new();
    
    [JsonPropertyName("query")]
    public List<PostmanQueryParameter>? Query { get; set; }
}

public class PostmanQueryParameter
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}

public class PostmanVariable
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class PostmanEvent
{
    [JsonPropertyName("listen")]
    public string Listen { get; set; } = string.Empty;
    
    [JsonPropertyName("script")]
    public PostmanScript? Script { get; set; }
}

public class PostmanScript
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("exec")]
    public List<string> Exec { get; set; } = new();
}
