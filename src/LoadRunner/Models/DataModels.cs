using System.Text.Json.Serialization;

namespace LoadRunner.Models;

public class ColumnMapping
{
    [JsonPropertyName("mappings")]
    public List<ColumnMappingEntry> Mappings { get; set; } = new();
    
    [JsonPropertyName("globalVariables")]
    public List<GlobalVariable> GlobalVariables { get; set; } = new();
}

public class ColumnMappingEntry
{
    [JsonPropertyName("csvColumn")]
    public string CsvColumn { get; set; } = string.Empty;
    
    [JsonPropertyName("postmanVariable")]
    public string PostmanVariable { get; set; } = string.Empty;
    
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "string";
    
    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }
}

public class GlobalVariable
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class CsvDataRow
{
    public Dictionary<string, string> Data { get; set; } = new();
}

public class TestDataRow
{
    public int BankId { get; set; }
    public int BranchId { get; set; }
    public int AccountId { get; set; }
    public string MinTransactionDate { get; set; } = string.Empty;
    public string MaxTransactionDate { get; set; } = string.Empty;
    public int AccountType { get; set; }
}
