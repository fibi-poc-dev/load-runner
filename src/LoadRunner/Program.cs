using LoadRunner.Models;
using LoadRunner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoadRunner;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("LoadRunner Performance Testing Tool");
            Console.WriteLine("===================================");

            var host = CreateHostBuilder(args).Build();
            
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("LoadRunner starting up...");

            // Validate configuration
            var configManager = services.GetRequiredService<Services.IConfigurationManager>();
            if (!await configManager.ValidateConfigurationAsync())
            {
                logger.LogError("Configuration validation failed. Exiting.");
                return 1;
            }

            // Test data loading
            var dataProvider = services.GetRequiredService<IDataProvider>();
            var testData = await dataProvider.LoadCsvDataAsync();
            logger.LogInformation("Loaded {Count} test data rows", testData.Count);

            // Test Postman collection loading
            var postmanCollection = configManager.LoadPostmanCollection();
            logger.LogInformation("Loaded Postman collection: {Name} with {Count} requests", 
                postmanCollection.Info.Name, postmanCollection.Items.Count);

            // Display sample data mapping
            if (testData.Count > 0)
            {
                Console.WriteLine("\nSample Data Mapping:");
                Console.WriteLine("===================");
                var sampleRow = testData[0];
                var variables = dataProvider.MapRowToVariables(sampleRow);
                var globalVars = dataProvider.GetGlobalVariables();
                
                Console.WriteLine("Mapped Variables:");
                foreach (var kvp in variables)
                {
                    Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
                }
                
                Console.WriteLine("\nGlobal Variables:");
                foreach (var kvp in globalVars)
                {
                    Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
                }
            }

            // Display Postman collection info
            Console.WriteLine($"\nPostman Collection: {postmanCollection.Info.Name}");
            Console.WriteLine("Available Requests:");
            foreach (var item in postmanCollection.Items)
            {
                Console.WriteLine($"  - {item.Name} ({item.Request.Method})");
            }

            Console.WriteLine("\nConfiguration loaded successfully. Ready to start load testing...");
            
            if (Console.IsInputRedirected)
            {
                // Handle piped input
                var input = Console.ReadLine();
                if (input?.ToUpper() == "Y")
                {
                    var loadTestEngine = services.GetRequiredService<ILoadTestEngine>();
                    var cts = new CancellationTokenSource();
                    
                    // Handle Ctrl+C gracefully
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                        Console.WriteLine("\nShutdown requested. Stopping test gracefully...");
                    };
                    
                    var success = await loadTestEngine.ExecuteLoadTestAsync(cts.Token);
                    return success ? 0 : 1;
                }
            }
            else
            {
                Console.WriteLine("Press 'y' to start the load test or any other key to exit...");
                var key = Console.ReadKey();
                Console.WriteLine();
                
                if (key.Key == ConsoleKey.Y)
                {
                    var loadTestEngine = services.GetRequiredService<ILoadTestEngine>();
                    var cts = new CancellationTokenSource();
                    
                    // Handle Ctrl+C gracefully
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                        Console.WriteLine("\nShutdown requested. Stopping test gracefully...");
                    };
                    
                    var success = await loadTestEngine.ExecuteLoadTestAsync(cts.Token);
                    return success ? 0 : 1;
                }
            }
            
            Console.WriteLine("Load test cancelled by user.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<LoadRunnerConfiguration>(
                    context.Configuration.GetSection("LoadRunner"));

                // Services
                services.AddSingleton<Services.IConfigurationManager, Services.ConfigurationManager>();
                services.AddSingleton<IDataProvider, DataProvider>();
                services.AddSingleton<ISuccessCriteriaValidator, SuccessCriteriaValidator>();
                services.AddSingleton<IHttpRequestBuilder, HttpRequestBuilder>();
                services.AddSingleton<IHttpClientManager, HttpClientManager>();
                services.AddSingleton<IPostmanScriptProcessor, PostmanScriptProcessor>();
                services.AddSingleton<IRequestSequenceManager, RequestSequenceManager>();
                services.AddSingleton<IMetricsCollector, MetricsCollector>();
                services.AddSingleton<IConsoleMonitor, ConsoleMonitor>();
                services.AddSingleton<IReportGenerator, ReportGenerator>();
                services.AddSingleton<ILoadTestEngine, LoadTestEngine>();
                
                // HTTP Client
                services.AddHttpClient();
                
                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                });
            });
}
