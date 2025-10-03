using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.IO.Pipelines;

namespace MetadataExplorerTest.Integration;

/// <summary>
/// Base class for integration tests that creates a real MCP server and client
/// connected through in-memory pipes. This allows testing the full request/response cycle.
/// </summary>
public abstract class McpServerIntegrationTestBase : IAsyncDisposable
{
    private readonly Pipe _clientToServerPipe = new();
    private readonly Pipe _serverToClientPipe = new();
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;

    protected McpServerIntegrationTestBase()
    {
        ServiceCollection sc = new();
        sc.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var mcpServerBuilder = sc
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "Test Server",
                    Version = "1.0.0"
                };
            })
            .WithStreamServerTransport(_clientToServerPipe.Reader.AsStream(), _serverToClientPipe.Writer.AsStream());
        
        ConfigureServices(sc, mcpServerBuilder);
        
        // validateScopes: true ensures scoped services are properly validated
        ServiceProvider = sc.BuildServiceProvider(validateScopes: true);

        _cts = new CancellationTokenSource();
        Server = ServiceProvider.GetRequiredService<McpServer>();
        _serverTask = Server.RunAsync(_cts.Token);
    }

    protected McpServer Server { get; }

    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Override this method to configure services and MCP server builder
    /// </summary>
    protected abstract void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder);

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _clientToServerPipe.Writer.Complete();
        _serverToClientPipe.Writer.Complete();

        await _serverTask;

        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a connected MCP client for testing
    /// </summary>
    protected async Task<McpClient> CreateMcpClientAsync(CancellationToken cancellationToken = default)
    {
        return await McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: _clientToServerPipe.Writer.AsStream(),
                _serverToClientPipe.Reader.AsStream(),
                loggerFactory: ServiceProvider.GetRequiredService<ILoggerFactory>()),
            loggerFactory: ServiceProvider.GetRequiredService<ILoggerFactory>(),
            cancellationToken: cancellationToken);
    }
}

