# LoadRunner Console Application - Architecture Document

## 1. Overview

The LoadRunner console application is a .NET 9.0 performance testing tool designed to execute load tests based on Postman collections with configurable parameters, data-driven testing capabilities, and real-time monitoring with comprehensive reporting.

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    LoadRunner Console App                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │   Configuration │  │    Data Layer   │  │   Execution     │  │
│  │     Manager     │  │                 │  │    Engine       │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │    Monitoring   │  │    Reporting    │  │   HTTP Client   │  │
│  │     Service     │  │    Generator    │  │     Manager     │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │  appsettings.   │  │ Postman Collection│  │ CSV Data File   │  │
│  │     json        │  │      (.json)     │  │                 │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ Column Mapping  │  │ Console Output  │  │ HTML Report     │  │
│  │    (.json)      │  │   (Real-time)   │  │   (Final)       │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Component Architecture

## 3. Core Components

### 3.1 Configuration Manager
**Responsibility**: Load and validate configuration from appsettings.json

**Key Features**:
- Load Postman collection file path
- Load CSV data file path
- Load JSON mapping configuration
- Load test execution settings
- Load performance thresholds

**Dependencies**: 
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Options

### 3.2 Data Layer
**Responsibility**: Handle data access and mapping operations

**Components**:
- **PostmanCollectionLoader**: Parse and load Postman collection files
- **CsvDataProvider**: Read and parse CSV referential data
- **ParameterMapper**: Map CSV columns to Postman collection parameters
- **DataIterator**: Manage data iteration for each test execution

**Dependencies**:
- CsvHelper
- System.Text.Json

### 3.3 Execution Engine
**Responsibility**: Orchestrate load test execution

**Components**:
- **LoadTestOrchestrator**: Main execution coordinator
- **IterationManager**: Manage test iterations and timing
- **ConcurrencyController**: Control parallel execution threads
- **TransactionRateController**: Maintain target TPS (Transactions Per Second)

**Key Features**:
- Execute requests based on Postman collection
- Apply data-driven parameters from CSV
- Control load patterns and timing
- Manage concurrent virtual users

### 3.4 HTTP Client Manager
**Responsibility**: Handle HTTP request execution

**Components**:
- **HttpClientFactory**: Create and manage HTTP clients
- **RequestBuilder**: Build HTTP requests from Postman collection
- **ResponseHandler**: Process and validate responses
- **SuccessCriteriaValidator**: Validate responses against defined success criteria
- **ConnectionPoolManager**: Optimize connection reuse

**Dependencies**:
- System.Net.Http
- Microsoft.Extensions.Http

### 3.5 Monitoring Service
**Responsibility**: Real-time performance monitoring and metrics collection

**Components**:
- **MetricsCollector**: Collect performance metrics
- **ConsoleDisplay**: Real-time console output
- **PerformanceCounters**: Track system resources
- **AlertManager**: Handle threshold violations

**Key Metrics**:
- Requests per second (RPS)
- Response time percentiles (50th, 90th, 95th, 99th)
- Error rates
- Throughput
- Memory and CPU usage

### 3.6 Reporting Generator
**Responsibility**: Generate final HTML reports

**Components**:
- **HtmlReportBuilder**: Create comprehensive HTML reports
- **ChartGenerator**: Generate performance charts
- **SummaryCalculator**: Calculate test summary statistics
- **TemplateEngine**: Apply HTML templates

**Dependencies**:
- System.Text.Json
- Custom HTML templating

## 4. Data Flow Architecture

```
┌─────────────────┐
│  appsettings.   │
│      json       │
└─────────┬───────┘
          │
          ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Configuration  │───▶│ Postman Collection│    │   CSV Data      │
│    Manager      │    │     Loader      │    │   Provider      │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          ▼                      ▼                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│    Parameter    │◀───┤   Data Layer    │───▶│   Parameter     │
│     Mapper      │    │   Integration   │    │    Iterator     │
└─────────┬───────┘    └─────────────────┘    └─────────┬───────┘
          │                                             │
          ▼                                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Execution Engine                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │Load Test        │  │  HTTP Client    │  │ Transaction     │ │
│  │Orchestrator     │◀─│    Manager      │──│Rate Controller  │ │
│  └─────────┬───────┘  └─────────────────┘  └─────────────────┘ │
└────────────┼───────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────┐    ┌─────────────────┐
│   Monitoring    │    │   Reporting     │
│    Service      │───▶│   Generator     │
│ (Real-time)     │    │ (Final HTML)    │
└─────────────────┘    └─────────────────┘
```

## 5. Configuration Structure

### 5.1 appsettings.json Schema

```json
{
  "LoadRunner": {
    "PostmanCollectionPath": "./collections/api-test.postman_collection.json",
    "CsvDataPath": "./data/test-data.csv",
    "ColumnMappingPath": "./mappings/column-mapping.json",
    "OutputSettings": {
      "HtmlReportPath": "./reports/load-test-results.html",
      "ConsoleUpdateIntervalMs": 1000
    },
    "ExecutionSettings": {
      "TestDurationMinutes": 10,
      "RampUpTimeMinutes": 2,
      "RampDownTimeMinutes": 1,
      "IterationSettings": [
        {
          "StepName": "LoginStep",
          "IntervalMinutes": 1,
          "Enabled": true,
          "SuccessCriteria": {
            "HttpStatusCodes": [200, 201],
            "ResponseTimeMaxMs": 2000,
            "ResponseBodyRegex": "\"status\":\\s*\"success\"",
            "ResponseHeaderChecks": [
              {
                "HeaderName": "Content-Type",
                "ExpectedValue": "application/json"
              }
            ],
            "JsonPathValidations": [
              {
                "JsonPath": "$.token",
                "ValidationRule": "NotNull"
              },
              {
                "JsonPath": "$.user.id",
                "ValidationRule": "IsNumeric"
              }
            ]
          }
        },
        {
          "StepName": "DataRetrievalStep",
          "IntervalMinutes": 2,
          "Enabled": true,
          "SuccessCriteria": {
            "HttpStatusCodes": [200],
            "ResponseTimeMaxMs": 1500,
            "ResponseBodyContains": ["data", "results"],
            "ResponseSizeMinBytes": 100,
            "ResponseSizeMaxBytes": 10240
          }
        }
      ]
    },
    "PerformanceSettings": {
      "TargetTransactionsPerSecond": 100,
      "MaxConcurrentUsers": 50,
      "RequestTimeoutMs": 30000,
      "MaxRetries": 3
    },
    "Thresholds": {
      "MaxResponseTimeMs": 2000,
      "MaxErrorRatePercent": 5,
      "MinTransactionsPerSecond": 80
    },
    "GlobalSuccessCriteria": {
      "DefaultHttpStatusCodes": [200, 201, 202, 204],
      "DefaultResponseTimeMaxMs": 5000,
      "IgnoreSslErrors": false,
      "FollowRedirects": true,
      "MaxRedirects": 5
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "LoadRunner": "Debug"
    }
  }
}
```

### 5.2 Column Mapping JSON Schema

```json
{
  "mappings": [
    {
      "csvColumn": "username",
      "postmanVariable": "{{username}}",
      "dataType": "string"
    },
    {
      "csvColumn": "user_id",
      "postmanVariable": "{{userId}}",
      "dataType": "integer"
    },
    {
      "csvColumn": "api_key",
      "postmanVariable": "{{apiKey}}",
      "dataType": "string",
      "encoding": "base64"
    }
  ],
  "globalVariables": [
    {
      "name": "baseUrl",
      "value": "https://api.example.com"
    }
  ]
}
```

## 6. Technology Stack

### 6.1 Core Technologies
- **.NET 9.0**: Primary framework
- **C# 13**: Programming language
- **System.Net.Http**: HTTP client operations
- **System.Text.Json**: JSON serialization/deserialization

### 6.2 Third-Party Libraries
- **CsvHelper**: CSV file processing
- **System.Text.RegularExpressions**: Regex pattern matching for response validation
- **System.Text.Json**: JSON processing and JsonPath validation
- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.DependencyInjection**: Dependency injection
- **Microsoft.Extensions.Logging**: Logging framework
- **Microsoft.Extensions.Http**: HTTP client factory

### 6.3 Development Tools
- **Visual Studio Code / Visual Studio**: IDE
- **NuGet**: Package management
- **xUnit**: Unit testing framework
- **Moq**: Mocking framework for testing

## 7. Performance Considerations

### 7.1 Scalability
- **Connection Pooling**: Reuse HTTP connections to minimize overhead
- **Async/Await**: Non-blocking operations for better concurrency
- **Thread Pool Management**: Efficient thread utilization
- **Memory Management**: Proper disposal of resources

### 7.2 Monitoring Metrics
- **Response Time Distribution**: Track P50, P90, P95, P99 percentiles
- **Throughput**: Requests per second and data transfer rates
- **Error Tracking**: HTTP error codes and exception counts
- **Resource Utilization**: CPU, memory, and network usage

### 7.3 Load Generation Strategy
- **Gradual Ramp-up**: Prevent overwhelming target systems
- **Steady State**: Maintain consistent load for specified duration
- **Controlled Ramp-down**: Graceful load reduction

## 8. Success Criteria Validation

### 8.1 Validation Framework
The LoadRunner application includes a comprehensive success criteria validation system that evaluates each HTTP response against configurable rules to determine transaction success or failure.

**SuccessCriteriaValidator Components**:
- **HttpStatusValidator**: Validate HTTP response status codes
- **ResponseTimeValidator**: Check response time thresholds
- **ResponseBodyValidator**: Validate response content using regex patterns
- **ResponseHeaderValidator**: Verify HTTP headers
- **JsonPathValidator**: Validate JSON responses using JsonPath expressions
- **ResponseSizeValidator**: Check response payload size constraints

### 8.2 Supported Validation Types

#### 8.2.1 HTTP Status Code Validation
```json
"HttpStatusCodes": [200, 201, 202, 204]
```
- Accepts array of acceptable HTTP status codes
- Default: [200, 201, 202, 204]
- Fails if response status not in allowed list

#### 8.2.2 Response Time Validation
```json
"ResponseTimeMaxMs": 2000
```
- Maximum acceptable response time in milliseconds
- Fails if response time exceeds threshold

#### 8.2.3 Response Body Regex Validation
```json
"ResponseBodyRegex": "\"status\":\\s*\"success\""
```
- Regular expression pattern to match in response body
- Uses .NET System.Text.RegularExpressions
- Fails if pattern not found

#### 8.2.4 Response Body Contains Validation
```json
"ResponseBodyContains": ["data", "results", "success"]
```
- Array of strings that must be present in response body
- Case-sensitive string matching
- Fails if any required string is missing

#### 8.2.5 Response Header Validation
```json
"ResponseHeaderChecks": [
  {
    "HeaderName": "Content-Type",
    "ExpectedValue": "application/json",
    "ValidationRule": "Equals"
  },
  {
    "HeaderName": "X-Request-ID",
    "ValidationRule": "NotNull"
  }
]
```
**Validation Rules**:
- `Equals`: Exact match
- `Contains`: Partial match
- `NotNull`: Header must be present
- `Regex`: Regular expression match

#### 8.2.6 JSON Path Validation
```json
"JsonPathValidations": [
  {
    "JsonPath": "$.token",
    "ValidationRule": "NotNull"
  },
  {
    "JsonPath": "$.user.id",
    "ValidationRule": "IsNumeric"
  },
  {
    "JsonPath": "$.status",
    "ValidationRule": "Equals",
    "ExpectedValue": "success"
  }
]
```
**Validation Rules**:
- `NotNull`: Field must exist and not be null
- `IsNumeric`: Field must be a number
- `IsString`: Field must be a string
- `Equals`: Field must equal expected value
- `GreaterThan`: Numeric comparison
- `LessThan`: Numeric comparison
- `Regex`: Regular expression match

#### 8.2.7 Response Size Validation
```json
"ResponseSizeMinBytes": 100,
"ResponseSizeMaxBytes": 10240
```
- Validate response payload size constraints
- Useful for detecting incomplete or oversized responses

### 8.3 Validation Result Handling

**Success Criteria Results**:
```csharp
public class ValidationResult
{
    public bool IsSuccess { get; set; }
    public List<string> FailureReasons { get; set; }
    public Dictionary<string, object> ValidationDetails { get; set; }
    public TimeSpan ValidationDuration { get; set; }
}
```

**Failure Actions**:
- Log detailed failure reasons
- Increment error counters
- Mark transaction as failed
- Continue with next iteration (configurable)
- Trigger alerts if error thresholds exceeded

### 8.4 Global vs Step-Specific Criteria

**Global Success Criteria**: Applied to all requests unless overridden
```json
"GlobalSuccessCriteria": {
  "DefaultHttpStatusCodes": [200, 201, 202, 204],
  "DefaultResponseTimeMaxMs": 5000,
  "IgnoreSslErrors": false,
  "FollowRedirects": true,
  "MaxRedirects": 5
}
```

**Step-Specific Criteria**: Override global settings for specific test steps
- Defined within each `IterationSettings` step
- Takes precedence over global criteria
- Allows fine-grained control per API endpoint

## 9. Output and Reporting

### 9.1 Real-time Console Output
```
LoadRunner Performance Test - Running
=====================================
Elapsed Time: 00:05:23
Current Load: 45 virtual users
Target TPS: 100 | Current TPS: 97.3
Response Times (ms): P50=145, P90=289, P95=456, P99=1234
Error Rate: 1.2% (23/1,897 requests)
Validation Failures: 0.8% (15/1,897 requests)
Memory Usage: 156 MB | CPU Usage: 23%

Success Criteria Summary:
- HTTP Status: 98.8% pass rate
- Response Time: 99.1% pass rate  
- Body Validation: 99.5% pass rate
- Header Validation: 100% pass rate

Last 10 Transactions:
[15:23:45] POST /api/login - 156ms - 200 OK - ✓ All validations passed
[15:23:45] GET /api/user/profile - 89ms - 200 OK - ✓ All validations passed
[15:23:46] POST /api/data/update - 234ms - 201 Created - ✓ All validations passed
[15:23:46] GET /api/data/list - 2156ms - 200 OK - ✗ Response time exceeded (max: 2000ms)
...
```

### 9.2 HTML Report Features
- **Executive Summary**: High-level test results and pass/fail status
- **Performance Charts**: Response time trends, throughput graphs
- **Success Criteria Analysis**: Detailed validation results and failure breakdown
- **Transaction Details**: Individual request/response analysis with validation status
- **Error Analysis**: Detailed error categorization and frequency
- **Validation Failure Report**: Success criteria violations with remediation suggestions
- **System Metrics**: Resource utilization during test execution
- **Test Configuration**: Complete test setup documentation including success criteria

## 9. Error Handling and Resilience

### 9.1 Error Categories
- **Configuration Errors**: Invalid settings, missing files
- **Data Errors**: CSV parsing issues, mapping conflicts
- **Network Errors**: Connection failures, timeouts
- **Application Errors**: HTTP 4xx/5xx responses
- **Validation Errors**: Success criteria failures, assertion mismatches

### 9.2 Resilience Patterns
- **Retry Logic**: Configurable retry attempts for failed requests
- **Circuit Breaker**: Prevent cascade failures
- **Graceful Degradation**: Continue testing with partial functionality
- **Resource Cleanup**: Proper disposal on errors and shutdown

## 10. Security Considerations

### 10.1 Data Protection
- **Sensitive Data Masking**: Hide credentials in logs and reports
- **Secure Configuration**: Environment-specific settings
- **Certificate Validation**: Proper SSL/TLS handling

### 10.2 Authentication Support
- **Bearer Token**: JWT token handling
- **Basic Authentication**: Username/password authentication
- **API Key**: Header-based API key authentication
- **OAuth 2.0**: OAuth flow support

## 11. Testing Strategy

### 11.1 Unit Testing
- **Configuration Loading**: Validate settings parsing
- **Data Mapping**: Test CSV to Postman variable mapping
- **HTTP Request Building**: Verify request construction
- **Success Criteria Validation**: Test all validation rules and edge cases
- **Metrics Calculation**: Test performance calculations

### 11.2 Integration Testing
- **End-to-End Scenarios**: Complete test execution flows
- **File I/O Operations**: Configuration and data file handling
- **Report Generation**: HTML output validation

## 12. Deployment and Usage

### 12.1 Prerequisites
- .NET 9.0 Runtime
- Valid Postman collection file
- CSV data file with test data
- Column mapping configuration

### 12.2 Command Line Usage
```bash
dotnet run --project LoadRunner.csproj
# or
./LoadRunner --config ./config/appsettings.json --verbose
```

### 12.3 Docker Support (Future Enhancement)
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
COPY . /app
WORKDIR /app
ENTRYPOINT ["dotnet", "LoadRunner.dll"]
```

## 13. Future Enhancements

### 13.1 Planned Features
- **Distributed Load Testing**: Multi-node execution
- **REST API**: Remote control and monitoring
- **Database Integration**: Direct database load testing
- **CI/CD Integration**: Jenkins, Azure DevOps plugins
- **Real-time Dashboard**: Web-based monitoring interface

### 13.2 Advanced Reporting
- **Historical Trending**: Compare results across test runs
- **Baseline Comparison**: Performance regression detection
- **Custom Metrics**: User-defined performance indicators
- **Export Formats**: PDF, Excel, CSV export options

---

*This architecture document serves as the foundation for the LoadRunner console application development and should be updated as the implementation evolves.*
