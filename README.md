# LoadRunner Performance Testing Tool

A high-performance, .NET-based load testing tool designed for API testing with Postman collection support.

## Features

✅ **Postman Collection Support** - Import and execute Postman collections
✅ **CSV Data-Driven Testing** - Support for parameterized testing with CSV data
✅ **Real-time Metrics** - Live console monitoring with smooth, non-blinking updates
✅ **Comprehensive Validation** - HTTP status, response time, JSON path, and body validations
✅ **HTML Reporting** - Detailed test reports with charts and graphs
✅ **WireMock Integration** - Built-in mock API support for testing
✅ **Flexible Configuration** - JSON-based configuration with millisecond precision
✅ **Multi-step Workflows** - Execute complex API workflows with dependencies

## Architecture

The LoadRunner consists of several key components:

- **LoadTestEngine** - Core test execution engine
- **ConfigurationManager** - Handles configuration validation and loading
- **HttpClientManager** - Manages HTTP connections and requests
- **MetricsCollector** - Collects and aggregates performance metrics
- **ConsoleMonitor** - Real-time console display with smooth updates
- **ReportGenerator** - Generates detailed HTML reports
- **PostmanScriptProcessor** - Processes Postman collections and variables

## Quick Start

### Prerequisites
- .NET 9.0 or later
- Docker (for WireMock)

### Running a Load Test

1. **Start the Mock API:**
   ```bash
   cd src
   docker-compose up -d
   ```

2. **Configure the test:**
   Edit `src/LoadRunner/appsettings.json`:
   ```json
   {
     "LoadRunner": {
       "ExecutionSettings": {
         "TestDurationMs": 300000,    // 5 minutes
         "RampUpTimeMs": 15000,       // 15 seconds
         "RampDownTimeMs": 15000      // 15 seconds
       },
       "PerformanceSettings": {
         "TargetTransactionsPerSecond": 5,
         "MaxConcurrentUsers": 5
       }
     }
   }
   ```

3. **Run the test:**
   ```bash
   cd src/LoadRunner
   dotnet run
   ```

4. **View results:**
   - Console: Real-time metrics during test execution
   - HTML Report: `src/LoadRunner/reports/load-test-results.html`

## Configuration

### Test Duration Settings (in milliseconds)
- `TestDurationMs`: Main test execution time
- `RampUpTimeMs`: Time to gradually increase load
- `RampDownTimeMs`: Time to gradually decrease load

### Performance Settings
- `TargetTransactionsPerSecond`: Target TPS
- `MaxConcurrentUsers`: Maximum virtual users
- `RequestTimeoutMs`: HTTP request timeout

### Step Configuration
Each API step can be individually configured:
```json
{
  "StepName": "jwt-server-si/token",
  "IntervalMs": 200,
  "Enabled": true,
  "SuccessCriteria": {
    "HttpStatusCodes": [200],
    "ResponseTimeMaxMs": 2000,
    "JsonPathValidations": [
      {
        "JsonPath": "$.jwt",
        "ValidationRule": "NotNull"
      }
    ]
  }
}
```

## Sample Test Results

Recent test execution achieved:
- **100% Success Rate** (0 failed requests)
- **Perfect TPS Target** (5.0 TPS achieved vs 5.0 target)
- **Excellent Performance** (P99 response time: 17ms)
- **All Validations Passed** (100% validation success rate)

## Project Structure

```
LoadRunner/
├── src/
│   ├── LoadRunner/           # Main application
│   │   ├── Models/           # Configuration and data models
│   │   ├── Services/         # Core services
│   │   ├── reports/          # Generated HTML reports
│   │   └── appsettings.json  # Main configuration
│   ├── MockBankingAPI/       # Sample .NET API (optional)
│   ├── MockData/             # WireMock mappings and data
│   ├── SampleData/           # Test data and Postman collections
│   └── docker-compose.yml    # WireMock container setup
└── README.md
```

## Key Improvements

### Performance Enhancements
- **Non-blinking Console**: Smooth real-time updates without screen flicker
- **Millisecond Precision**: All timing configurations use milliseconds for precision
- **Optimized Task Management**: Proper task completion with timeouts
- **Reduced Jitter**: Minimized random delays for consistent performance

### Validation Improvements
- **Fixed AccountType Validation**: Updated to match actual API responses
- **Comprehensive Success Criteria**: Multiple validation layers
- **Flexible Validation Rules**: JSON path, regex, and content validations

### Timing Accuracy
- **Precise Duration Control**: Tests complete within expected timeframes
- **Graceful Shutdown**: 10-second timeout for task completion
- **Improved Phase Management**: Better ramp-up/steady/ramp-down transitions

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is for internal POC development.
