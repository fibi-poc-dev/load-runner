using LoadRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LoadRunner.Services;

public interface IConfigurationManager
{
    LoadRunnerConfiguration Configuration { get; }
    PostmanCollection LoadPostmanCollection();
    ColumnMapping LoadColumnMapping();
    Task<bool> ValidateConfigurationAsync();
}

public class ConfigurationManager : IConfigurationManager
{
    private readonly LoadRunnerConfiguration _configuration;
    private readonly ILogger<ConfigurationManager> _logger;

    public ConfigurationManager(IOptions<LoadRunnerConfiguration> configuration, ILogger<ConfigurationManager> logger)
    {
        _configuration = configuration.Value;
        _logger = logger;
    }

    public LoadRunnerConfiguration Configuration => _configuration;

    public PostmanCollection LoadPostmanCollection()
    {
        try
        {
            _logger.LogInformation("Loading Postman collection from: {Path}", _configuration.PostmanCollectionPath);
            
            if (!File.Exists(_configuration.PostmanCollectionPath))
            {
                throw new FileNotFoundException($"Postman collection file not found: {_configuration.PostmanCollectionPath}");
            }

            var jsonContent = File.ReadAllText(_configuration.PostmanCollectionPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var collection = JsonSerializer.Deserialize<PostmanCollection>(jsonContent, options);
            if (collection == null)
            {
                throw new InvalidOperationException("Failed to deserialize Postman collection");
            }

            _logger.LogInformation("Successfully loaded Postman collection: {Name} with {Count} items", 
                collection.Info.Name, collection.Items.Count);

            return collection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Postman collection from {Path}", _configuration.PostmanCollectionPath);
            throw;
        }
    }

    public ColumnMapping LoadColumnMapping()
    {
        try
        {
            _logger.LogInformation("Loading column mapping from: {Path}", _configuration.ColumnMappingPath);
            
            if (!File.Exists(_configuration.ColumnMappingPath))
            {
                throw new FileNotFoundException($"Column mapping file not found: {_configuration.ColumnMappingPath}");
            }

            var jsonContent = File.ReadAllText(_configuration.ColumnMappingPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var mapping = JsonSerializer.Deserialize<ColumnMapping>(jsonContent, options);
            if (mapping == null)
            {
                throw new InvalidOperationException("Failed to deserialize column mapping");
            }

            _logger.LogInformation("Successfully loaded column mapping with {MappingCount} mappings and {VariableCount} global variables", 
                mapping.Mappings.Count, mapping.GlobalVariables.Count);

            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load column mapping from {Path}", _configuration.ColumnMappingPath);
            throw;
        }
    }

    public async Task<bool> ValidateConfigurationAsync()
    {
        var isValid = true;
        var validationErrors = new List<string>();

        // Validate required file paths
        if (string.IsNullOrWhiteSpace(_configuration.PostmanCollectionPath))
        {
            validationErrors.Add("PostmanCollectionPath is required");
            isValid = false;
        }
        else if (!File.Exists(_configuration.PostmanCollectionPath))
        {
            validationErrors.Add($"Postman collection file not found: {_configuration.PostmanCollectionPath}");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(_configuration.CsvDataPath))
        {
            validationErrors.Add("CsvDataPath is required");
            isValid = false;
        }
        else if (!File.Exists(_configuration.CsvDataPath))
        {
            validationErrors.Add($"CSV data file not found: {_configuration.CsvDataPath}");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(_configuration.ColumnMappingPath))
        {
            validationErrors.Add("ColumnMappingPath is required");
            isValid = false;
        }
        else if (!File.Exists(_configuration.ColumnMappingPath))
        {
            validationErrors.Add($"Column mapping file not found: {_configuration.ColumnMappingPath}");
            isValid = false;
        }

        // Validate performance settings
        if (_configuration.PerformanceSettings.TargetTransactionsPerSecond <= 0)
        {
            validationErrors.Add("TargetTransactionsPerSecond must be greater than 0");
            isValid = false;
        }

        if (_configuration.PerformanceSettings.MaxConcurrentUsers <= 0)
        {
            validationErrors.Add("MaxConcurrentUsers must be greater than 0");
            isValid = false;
        }

        if (_configuration.ExecutionSettings.TestDurationMs <= 0)
        {
            validationErrors.Add("TestDurationMs must be greater than 0");
            isValid = false;
        }

        // Validate output directory
        var reportDirectory = Path.GetDirectoryName(_configuration.OutputSettings.HtmlReportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory) && !Directory.Exists(reportDirectory))
        {
            try
            {
                Directory.CreateDirectory(reportDirectory);
                _logger.LogInformation("Created output directory: {Directory}", reportDirectory);
            }
            catch (Exception ex)
            {
                validationErrors.Add($"Failed to create output directory: {ex.Message}");
                isValid = false;
            }
        }

        if (!isValid)
        {
            foreach (var error in validationErrors)
            {
                _logger.LogError("Configuration validation error: {Error}", error);
            }
        }
        else
        {
            _logger.LogInformation("Configuration validation passed");
        }

        return isValid;
    }
}
