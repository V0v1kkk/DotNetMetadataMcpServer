using System.Reflection;
using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Services;
using DotNetMetadataMcpServer.Tools;
using Serilog;

namespace DotNetMetadataMcpServer;

// ReSharper disable once UnusedType.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class Program
{
    /// <summary>
    /// DotNet Metadata MCP Server
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || args[0] != "--homeEnvVariable" || string.IsNullOrWhiteSpace(args[1]))
        {
            Console.WriteLine("The --homeEnvVariable argument with a value is required");
            return 1;
        } 
        
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        var homeEnvVariable = args[1];
        Environment.SetEnvironmentVariable("HOME", homeEnvVariable);
        
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("RunId", Guid.NewGuid())
            .CreateLogger();
        
        Log.Logger = logger; 
        
        try
        {
            logger.Information("Starting the server");
            
            var builder = Host.CreateApplicationBuilder(args);
            
            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(logger);
            builder.Logging.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
            
            // Configure MCP server
            builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
                {
                    Name = "DotNet Projects Types Explorer MCP Server",
                    Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithTools<AssemblyTools>()
            .WithTools<NamespaceTools>()
            .WithTools<TypeTools>()
            .WithTools<NuGetTools>();
            
            // Register configuration
            builder.Services.Configure<ToolsConfiguration>(configuration.GetSection(ToolsConfiguration.SectionName));
            
            // Register services as scoped (per request)
            builder.Services.AddScoped<MsBuildHelper>();
            builder.Services.AddScoped<ReflectionTypesCollector>();
            builder.Services.AddScoped<IDependenciesScanner, DependenciesScanner>();
            builder.Services.AddScoped<AssemblyToolService>();
            builder.Services.AddScoped<NamespaceToolService>();
            builder.Services.AddScoped<TypeToolService>();
            builder.Services.AddScoped<NuGetToolService>();
            
            var host = builder.Build();
            await host.RunAsync();

            return 0;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while running the server");
            Console.WriteLine(ex);
            
            return 1;
        }
        finally
        {
            logger.Information("Shutting down the server");
            await Log.CloseAndFlushAsync();
        }
    }
}
