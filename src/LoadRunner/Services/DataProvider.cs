using CsvHelper;
using CsvHelper.Configuration;
using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace LoadRunner.Services;

public interface IDataProvider
{
    Task<List<CsvDataRow>> LoadCsvDataAsync();
    Task<List<TestDataRow>> LoadTypedDataAsync();
    ColumnMapping GetColumnMapping();
    Dictionary<string, string> MapRowToVariables(CsvDataRow dataRow);
    Dictionary<string, string> GetGlobalVariables();
    Dictionary<string, string> GetPostmanCollectionVariables();
}

public class DataProvider : IDataProvider
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<DataProvider> _logger;
    private ColumnMapping? _columnMapping;

    public DataProvider(IConfigurationManager configurationManager, ILogger<DataProvider> logger)
    {
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public async Task<List<CsvDataRow>> LoadCsvDataAsync()
    {
        try
        {
            var csvPath = _configurationManager.Configuration.CsvDataPath;
            _logger.LogInformation("Loading CSV data from: {Path}", csvPath);

            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"CSV data file not found: {csvPath}");
            }

            var dataRows = new List<CsvDataRow>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null, // Ignore missing fields
                BadDataFound = null // Ignore bad data
            };

            using var reader = new StringReader(await File.ReadAllTextAsync(csvPath));
            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? throw new InvalidOperationException("No headers found in CSV file");

            _logger.LogInformation("CSV headers found: {Headers}", string.Join(", ", headers));

            while (await csv.ReadAsync())
            {
                // Skip empty rows
                if (csv.Parser.Record == null || csv.Parser.Record.All(string.IsNullOrWhiteSpace))
                    continue;
                    
                var dataRow = new CsvDataRow();
                
                foreach (var header in headers)
                {
                    var value = csv.GetField(header) ?? string.Empty;
                    dataRow.Data[header] = value;
                }

                dataRows.Add(dataRow);
            }

            _logger.LogInformation("Successfully loaded {Count} data rows from CSV", dataRows.Count);
            return dataRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load CSV data");
            throw;
        }
    }

    public async Task<List<TestDataRow>> LoadTypedDataAsync()
    {
        try
        {
            var csvPath = _configurationManager.Configuration.CsvDataPath;
            _logger.LogInformation("Loading typed data from: {Path}", csvPath);

            var testData = new List<TestDataRow>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null, // Ignore missing fields
                BadDataFound = null // Ignore bad data
            };

            using var reader = new StringReader(await File.ReadAllTextAsync(csvPath));
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<TestDataRow>().ToList();
            
            _logger.LogInformation("Successfully loaded {Count} typed data rows", records.Count);
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load typed data");
            throw;
        }
    }

    public ColumnMapping GetColumnMapping()
    {
        if (_columnMapping == null)
        {
            _columnMapping = _configurationManager.LoadColumnMapping();
        }
        return _columnMapping;
    }

    public Dictionary<string, string> MapRowToVariables(CsvDataRow dataRow)
    {
        var mapping = GetColumnMapping();
        var variables = new Dictionary<string, string>();

        foreach (var mappingEntry in mapping.Mappings)
        {
            if (dataRow.Data.TryGetValue(mappingEntry.CsvColumn, out var value))
            {
                var processedValue = ProcessValueByDataType(value, mappingEntry.DataType, mappingEntry.Encoding);
                
                // Remove the double braces from the postman variable format
                var variableName = mappingEntry.PostmanVariable.Trim('{', '}');
                variables[variableName] = processedValue;
            }
            else
            {
                _logger.LogWarning("CSV column '{Column}' not found in data row", mappingEntry.CsvColumn);
            }
        }

        _logger.LogDebug("Mapped {Count} variables from data row", variables.Count);
        return variables;
    }

    public Dictionary<string, string> GetGlobalVariables()
    {
        // First get variables from Postman collection
        var collectionVars = GetPostmanCollectionVariables();
        
        // Then get any additional variables from column mapping (for backwards compatibility)
        var mapping = GetColumnMapping();
        var mappingVars = new Dictionary<string, string>();

        foreach (var globalVar in mapping.GlobalVariables)
        {
            mappingVars[globalVar.Name] = globalVar.Value;
        }

        // Collection variables take precedence over mapping variables
        foreach (var kvp in mappingVars)
        {
            if (!collectionVars.ContainsKey(kvp.Key))
            {
                collectionVars[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogDebug("Retrieved {Count} global variables ({CollectionCount} from collection, {MappingCount} from mapping)", 
            collectionVars.Count, GetPostmanCollectionVariables().Count, mapping.GlobalVariables.Count);
        
        return collectionVars;
    }

    public Dictionary<string, string> GetPostmanCollectionVariables()
    {
        var collection = _configurationManager.LoadPostmanCollection();
        var variables = new Dictionary<string, string>();

        foreach (var variable in collection.Variables)
        {
            // Only include variables that have non-empty values
            if (!string.IsNullOrWhiteSpace(variable.Value))
            {
                variables[variable.Key] = variable.Value;
            }
        }

        _logger.LogDebug("Retrieved {Count} variables from Postman collection", variables.Count);
        return variables;
    }

    private string ProcessValueByDataType(string value, string dataType, string? encoding)
    {
        try
        {
            var processedValue = dataType.ToLowerInvariant() switch
            {
                "integer" => int.Parse(value).ToString(),
                "double" => double.Parse(value).ToString(CultureInfo.InvariantCulture),
                "boolean" => bool.Parse(value).ToString().ToLowerInvariant(),
                "datetime" => DateTime.Parse(value).ToString("yyyy-MM-dd"),
                "string" => value,
                _ => value
            };

            if (!string.IsNullOrWhiteSpace(encoding))
            {
                processedValue = encoding.ToLowerInvariant() switch
                {
                    "base64" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(processedValue)),
                    "url" => Uri.EscapeDataString(processedValue),
                    _ => processedValue
                };
            }

            return processedValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process value '{Value}' as {DataType}, using original value", value, dataType);
            return value;
        }
    }
}
