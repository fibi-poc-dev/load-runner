# LoadRunner Performance Testing Framework

A comprehensive, high-performance load testing framework built with .NET 9.0, designed for API performance testing with advanced analytics and reporting capabilities.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Supported-blue.svg)](https://www.docker.com/)

## 🚀 Features

### Core Capabilities
- **High-Performance Load Testing** - Achieve precise TPS targets with millisecond accuracy
- **Postman Collection Integration** - Import and execute Postman collections seamlessly
- **CSV Data-Driven Testing** - Support for dynamic data injection from CSV files
- **Advanced Validation** - JSON path validation, response time checks, and custom success criteria
- **Real-Time Monitoring** - Live console dashboard with performance metrics
- **Comprehensive Reporting** - HTML reports with interactive charts and per-API breakdowns

### Advanced Analytics
- **Per-API Performance Breakdown** - Detailed metrics for each endpoint
- **Interactive Visualizations** - 6 different chart types including spider charts
- **Response Time Percentiles** - P50, P90, P95, P99 calculations
- **Success Rate Analysis** - Color-coded success indicators
- **Error Analysis** - Detailed error breakdown and categorization

### Technical Features
- **WireMock Integration** - Mock API responses for testing
- **Configurable Load Patterns** - Ramp-up, steady-state, and ramp-down phases
- **Concurrent User Simulation** - Multi-threaded execution
- **Graceful Shutdown** - Clean test termination with proper resource cleanup
- **Memory & CPU Monitoring** - Real-time system resource tracking

## 📋 Table of Contents

- [Quick Start](#-quick-start)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Usage](#-usage)
- [API Testing](#-api-testing)
- [Data Management](#-data-management)
- [Reporting](#-reporting)
- [Advanced Features](#-advanced-features)
- [Troubleshooting](#-troubleshooting)
- [Contributing](#-contributing)
- [License](#-license)

## ⚡ Quick Start

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/get-started) (for WireMock)
- [Git](https://git-scm.com/)

### 1. Clone the Repository
```bash
git clone https://github.com/fibi-poc-dev/load-runner.git
cd load-runner/src
```

### 2. Start WireMock (Mock API Server)
```bash
docker-compose up -d
```

### 3. Run Load Test
```bash
cd LoadRunner
dotnet run
# Press 'y' when prompted to start the test
```

### 4. View Results
- **Console**: Real-time metrics during test execution
- **HTML Report**: `./reports/load-test-results.html` (automatically opens in default browser)
- **Interactive Charts**: 6 different visualization types with per-API breakdowns

## 🔧 Installation

### Development Setup
```bash
# Clone repository
git clone https://github.com/fibi-poc-dev/load-runner.git
cd load-runner/src

# Restore dependencies
cd LoadRunner
dotnet restore

# Build project
dotnet build

# Run tests
dotnet run
```

### Docker Setup
```bash
# Start WireMock container
docker-compose up -d

# Verify WireMock is running
curl http://localhost:8080/__admin/health
```

### Production Deployment
```bash
# Build release version
dotnet build -c Release

# Publish for deployment
dotnet publish -c Release -o ./publish

# Run published version
cd publish
./LoadRunner
```

## ⚙️ Configuration

### Main Configuration File: `appsettings.json`

```json
{
  "LoadRunner": {
    "PostmanCollectionPath": "../SampleData/collection.json",
    "CsvDataPath": "../SampleData/referential_data.csv",
    "ColumnMappingPath": "../SampleData/column-mapping.json",
    "ExecutionSettings": {
      "TestDurationMs": 300000,     // 5 minutes
      "RampUpTimeMs": 15000,        // 15 seconds
      "RampDownTimeMs": 15000,      // 15 seconds
      "IterationSettings": [
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
      ]
    },
    "PerformanceSettings": {
      "TargetTransactionsPerSecond": 5,
      "MaxConcurrentUsers": 5,
      "RequestTimeoutMs": 10000,
      "MaxRetries": 3
    },
    "Thresholds": {
      "MaxResponseTimeMs": 5000,
      "MaxErrorRatePercent": 10,
      "MinTransactionsPerSecond": 1
    }
  }
}
```

### Key Configuration Sections

#### Execution Settings
- `TestDurationMs`: Total test duration in milliseconds
- `RampUpTimeMs`: Time to gradually increase load
- `RampDownTimeMs`: Time to gradually decrease load
- `IterationSettings`: Per-API endpoint configuration

#### Performance Settings
- `TargetTransactionsPerSecond`: Desired TPS target
- `MaxConcurrentUsers`: Maximum concurrent virtual users
- `RequestTimeoutMs`: HTTP request timeout
- `MaxRetries`: Number of retry attempts for failed requests

#### Success Criteria
- `HttpStatusCodes`: Expected HTTP status codes
- `ResponseTimeMaxMs`: Maximum acceptable response time
- `JsonPathValidations`: JSON response validation rules
- `ResponseBodyContains`: Required text in response body

### Understanding TPS and Concurrent Users Relationship

The relationship between `TargetTransactionsPerSecond` and `MaxConcurrentUsers` is crucial for effective load testing:

#### Mathematical Relationship
```
TPS = (MaxConcurrentUsers × Transactions per User per Second)

Where: Transactions per User per Second = 1 / (Total Iteration Time in Seconds)
```

#### Example Calculation (Current Config)
```
Steps per iteration: 5 enabled steps
Interval per step: 200ms each
Total iteration time: 5 × 200ms = 1,000ms = 1 second
Transactions per user per second: 1 TPS per user
Expected total TPS: 5 users × 1 TPS = 5 TPS ✓
```

#### Configuration Scenarios

| Goal TPS | Recommended Users | Interval per Step | Use Case |
|----------|------------------|-------------------|----------|
| 5 TPS    | 5 users         | 200ms            | Baseline testing |
| 10 TPS   | 10 users        | 200ms            | Scale users approach |
| 10 TPS   | 5 users         | 100ms            | Faster iterations approach |
| 20 TPS   | 20 users        | 200ms            | High concurrency testing |

#### Design Principles

**For Realistic User Simulation:**
```json
{
  "TargetTransactionsPerSecond": 10,
  "MaxConcurrentUsers": 10,
  "IntervalMs": 200  // Simulates user think time
}
```

**For Maximum Throughput Testing:**
```json
{
  "TargetTransactionsPerSecond": 50,
  "MaxConcurrentUsers": 25,
  "IntervalMs": 100  // Aggressive load testing
}
```

**Key Considerations:**
- **Response Time Impact**: Slower API responses reduce actual TPS below target
- **Resource Limits**: Monitor test runner capacity for high concurrent loads
- **Realistic Behavior**: Balance between user simulation and system stress testing

## 🎮 Usage

### Basic Load Test
```bash
cd LoadRunner
dotnet run
# Press 'y' to start
# Press 'Ctrl+C' to stop gracefully
```

### Custom Configuration
```bash
# Use different settings file
dotnet run --environment Production

# Override specific settings
export LoadRunner__PerformanceSettings__TargetTransactionsPerSecond=10
dotnet run
```

### Command Line Options
```bash
# Run with verbose logging
dotnet run -- --verbose

# Specify custom collection path
dotnet run -- --collection "../custom/collection.json"

# Set custom duration (in seconds)
dotnet run -- --duration 600
```

## 🌐 API Testing

### Supported API Features

#### Authentication
- **JWT Token Generation**: Automatic token refresh
- **OAuth 2.0**: Password grant flow support
- **Custom Headers**: Configurable authentication headers

#### Request Types
- **GET**: Query parameters and headers
- **POST**: JSON payloads and form data
- **PUT/PATCH**: Update operations
- **DELETE**: Resource cleanup

#### Validation Options
- **HTTP Status Codes**: Expected response codes
- **Response Time**: Maximum acceptable latency
- **JSON Path**: Specific field validation
- **Response Body**: Text content verification
- **Headers**: Required response headers

### Example API Endpoints
The framework includes sample endpoints for banking API testing:

1. **JWT Token Generation** (`/jwt-server-si/token`)
2. **OAuth Token** (`/sso-portal/token`)
3. **Account Transactions** (`/accountTransactions`)
4. **Account Type** (`/accountType`)
5. **Cache Cleanup** (`/cleanRedis`)

## 📊 Data Management

### CSV Data Integration
```csv
BankId,BranchId,AccountId,MinTransactionDate,MaxTransactionDate,AccountType
99,999,999999,2023-10-05,2023-10-05,105
31,39,100609,2023-10-01,2023-10-31,1
```

### Column Mapping
```json
{
  "columnMappings": {
    "BankId": "bankId",
    "BranchId": "branchId",
    "AccountId": "accountId",
    "MinTransactionDate": "startDate",
    "MaxTransactionDate": "endDate",
    "AccountType": "accountType"
  }
}
```

### Variable Injection
Variables from CSV are automatically injected into:
- **URL Parameters**: `{{BankId}}`, `{{AccountId}}`
- **Request Body**: JSON field replacement
- **Headers**: Dynamic header values
- **Query Parameters**: URL query strings

## 📈 Reporting

### Console Dashboard
Real-time monitoring includes:
- **Current TPS**: Live transaction rate
- **Response Times**: P50, P90, P95, P99 percentiles
- **Error Rate**: Success/failure percentage
- **Memory Usage**: System resource consumption
- **Recent Transactions**: Last 10 API calls with status

### HTML Reports
Comprehensive HTML reports with:

**Automatic Browser Opening**: Reports automatically open in your default OS browser upon test completion (Windows, macOS, Linux supported).

#### Executive Summary
- Overall test status (PASS/FAIL)
- Test duration and total requests
- Success rates and error analysis

#### Interactive Charts (6 Types)
1. **Response Time Timeline** - Performance over time
2. **Throughput Chart** - Requests per minute
3. **Per-API Response Time** - Comparison across endpoints
4. **Spider Chart** - Multi-dimensional performance view
5. **Request Distribution** - Traffic allocation pie chart
6. **Success Rate by API** - Endpoint reliability comparison

#### Per-API Breakdown
- **Summary Table**: Response times, success rates, validation metrics
- **Detailed Analysis**: Individual endpoint statistics
- **Error Analysis**: Failure categorization and frequency

### Sample Report Structure
```
LoadRunner Performance Test Report
├── Executive Summary
├── Performance Metrics
├── Performance Charts
│   ├── Overall Response Time Timeline
│   ├── Overall Throughput
│   ├── Per-API Response Time Comparison
│   ├── API Performance Spider Chart
│   ├── API Request Distribution
│   └── Success Rate by API
├── Per-API Performance Breakdown
├── Success Criteria Analysis
├── Error Analysis (if applicable)
└── Test Configuration
```

## 🔬 Advanced Features

### Load Testing Patterns

#### Ramp-Up Pattern
```
Users
  ^
  |     /‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾\
  |    /                     \
  |   /                       \
  |  /                         \
  | /                           \
  |/                             \
  +-------------------------------→ Time
  Ramp-up   Steady-state   Ramp-down
```

#### Custom Load Profiles
- **Spike Testing**: Sudden load increases
- **Stress Testing**: Gradual load increase to failure point
- **Volume Testing**: Large data set processing
- **Endurance Testing**: Extended duration runs

### Performance Optimization

#### HTTP Client Pool Management
- Connection reuse and pooling
- DNS resolution caching
- Keep-alive connections
- Compression handling

#### Memory Management
- Efficient data structure usage
- Garbage collection optimization
- Resource cleanup automation
- Memory leak prevention

#### Concurrency Control
- Thread-safe operations
- Lock-free algorithms where possible
- Async/await pattern usage
- Cancellation token support

### Monitoring Integration

#### System Metrics
- CPU utilization tracking
- Memory consumption monitoring
- Network I/O statistics
- Disk usage analysis

#### Custom Metrics
- Business KPI tracking
- SLA compliance monitoring
- Custom validation rules
- Third-party integrations

## 🧪 WireMock Integration

### Mock Server Setup
WireMock provides realistic API responses for testing:

```yaml
# docker-compose.yml
services:
  wiremock:
    image: wiremock/wiremock:3.3.1
    ports:
      - "8080:8080"
    volumes:
      - ./MockData:/home/wiremock
    command: --global-response-templating --verbose
```

### Mock Data Structure
```
MockData/
├── mappings/           # API endpoint definitions
│   ├── jwt-token.json
│   ├── oauth-token.json
│   ├── account-transactions.json
│   └── account-type.json
└── __files/           # Response templates
    ├── transactions.json
    └── account-info.json
```

### Sample Mock Mapping
```json
{
  "request": {
    "method": "POST",
    "url": "/jwt-server-si/token",
    "headers": {
      "Content-Type": {
        "equalTo": "application/json"
      }
    }
  },
  "response": {
    "status": 200,
    "headers": {
      "Content-Type": "application/json"
    },
    "bodyFileName": "jwt-response.json",
    "transformers": ["response-template"]
  }
}
```

## 🔍 Troubleshooting

### Common Issues

#### Connection Errors
```bash
# Check WireMock status
docker-compose ps
curl http://localhost:8080/__admin/health

# Restart WireMock
docker-compose restart wiremock
```

#### Performance Issues
```bash
# Check system resources
top -p $(pgrep -f LoadRunner)

# Monitor network connections
netstat -an | grep 8080

# Check Docker container logs
docker-compose logs wiremock
```

#### Configuration Problems
```bash
# Validate JSON configuration
cat appsettings.json | jq .

# Check file permissions
ls -la ../SampleData/

# Verify paths
find . -name "collection.json"
```

### Debug Mode
Enable detailed logging in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "LoadRunner": "Debug"
    }
  }
}
```

### Performance Tuning

#### For High TPS Testing
```json
{
  "PerformanceSettings": {
    "TargetTransactionsPerSecond": 100,
    "MaxConcurrentUsers": 50,
    "RequestTimeoutMs": 5000
  }
}
```

#### For Stability Testing
```json
{
  "ExecutionSettings": {
    "TestDurationMs": 3600000,  // 1 hour
    "RampUpTimeMs": 300000,     // 5 minutes
    "RampDownTimeMs": 300000    // 5 minutes
  }
}
```

## 🏗️ Architecture

### System Components
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   LoadRunner    │    │    WireMock      │    │   Test Data     │
│                 │    │                  │    │                 │
│ ┌─────────────┐ │    │ ┌──────────────┐ │    │ ┌─────────────┐ │
│ │ Test Engine │ ├────┤ │ Mock APIs    │ │    │ │ CSV Files   │ │
│ └─────────────┘ │    │ └──────────────┘ │    │ └─────────────┘ │
│                 │    │                  │    │                 │
│ ┌─────────────┐ │    │ ┌──────────────┐ │    │ ┌─────────────┐ │
│ │ Metrics     │ │    │ │ Mappings     │ │    │ │ Collections │ │
│ │ Collector   │ │    │ │ & Responses  │ │    │ │ (Postman)   │ │
│ └─────────────┘ │    │ └──────────────┘ │    │ └─────────────┘ │
│                 │    │                  │    │                 │
│ ┌─────────────┐ │    └──────────────────┘    └─────────────────┘
│ │ Report      │ │
│ │ Generator   │ │
│ └─────────────┘ │
└─────────────────┘
```

### Class Structure
```
LoadRunner/
├── Models/
│   ├── Configuration.cs          # Application settings
│   ├── DataModels.cs            # Data structures
│   ├── PostmanModels.cs         # Postman integration
│   └── ResultModels.cs          # Test results
├── Services/
│   ├── ConfigurationManager.cs  # Settings management
│   ├── ConsoleMonitor.cs        # Real-time display
│   ├── DataProvider.cs          # CSV data handling
│   ├── HttpClientManager.cs     # HTTP operations
│   ├── HttpRequestBuilder.cs    # Request construction
│   ├── LoadTestEngine.cs        # Core test engine
│   ├── MetricsCollector.cs      # Performance tracking
│   ├── PostmanScriptProcessor.cs # Postman compatibility
│   ├── ReportGenerator.cs       # HTML report creation
│   ├── RequestSequenceManager.cs # API workflow
│   └── SuccessCriteriaValidator.cs # Validation logic
└── Program.cs                   # Application entry point
```

## 🤝 Contributing

### Development Workflow
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Commit your changes (`git commit -m 'Add amazing feature'`)
7. Push to the branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

### Code Standards
- Follow C# coding conventions
- Include XML documentation for public APIs
- Write unit tests for new features
- Ensure performance benchmarks pass
- Update documentation as needed

### Testing Guidelines
- Unit tests for individual components
- Integration tests for API interactions
- Performance tests for load scenarios
- Mock external dependencies
- Maintain test coverage above 80%

## 📝 Changelog

### Version 1.2.0 (Latest)
- ✨ Added comprehensive per-API breakdown reporting
- 📊 Enhanced charts with 6 different visualization types
- 🕸️ Introduced spider chart for multi-dimensional analysis
- 🎨 Improved HTML report styling and interactivity
- 🧹 Repository cleanup and optimization

### Version 1.1.0
- 🚀 Performance improvements for high TPS scenarios
- 🔧 Enhanced configuration validation
- 📱 Real-time console monitoring improvements
- 🐛 Bug fixes for timing accuracy

### Version 1.0.0
- 🎉 Initial release
- ⚡ Core load testing functionality
- 📊 Basic reporting capabilities
- 🔌 WireMock integration

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

### Documentation
- [Wiki](https://github.com/fibi-poc-dev/load-runner/wiki)
- [API Documentation](https://github.com/fibi-poc-dev/load-runner/blob/main/docs/api.md)
- [Examples](https://github.com/fibi-poc-dev/load-runner/tree/main/examples)

### Community
- [Issues](https://github.com/fibi-poc-dev/load-runner/issues) - Bug reports and feature requests
- [Discussions](https://github.com/fibi-poc-dev/load-runner/discussions) - General questions and ideas
- [Stack Overflow](https://stackoverflow.com/questions/tagged/loadrunner-framework) - Technical questions

### Commercial Support
For enterprise support, custom features, or consulting services, contact: [support@loadrunner-framework.com](mailto:support@loadrunner-framework.com)

---

**Built with ❤️ using .NET 9.0 | Performance Testing Made Simple**
