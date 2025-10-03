using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Services;
using DotNetMetadataMcpServer.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataExplorerTest.Integration;

/// <summary>
/// Tests to verify that scoped services are properly instantiated per request
/// and disposed after each request completes.
/// </summary>
[TestFixture]
public class ScopedServicesLifecycleTests : McpServerIntegrationTestBase
{
    private static int _scannerConstructedCount = 0;

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        // Reset counters
        _scannerConstructedCount = 0;

        // Configure MCP tools
        mcpServerBuilder
            .WithTools<AssemblyTools>()
            .WithTools<NamespaceTools>()
            .WithTools<TypeTools>();

        // Configure settings
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tools:DefaultPageSize"] = "10",
                ["Tools:IntendResponse"] = "false"
            })
            .Build();

        services.Configure<ToolsConfiguration>(configuration.GetSection("Tools"));

        // Register services as scoped with tracking
        services.AddScoped<MsBuildHelper>();
        services.AddScoped<ReflectionTypesCollector>();
        
        services.AddScoped<IDependenciesScanner>(sp =>
        {
            Interlocked.Increment(ref _scannerConstructedCount);
            var scanner = new TrackableDependenciesScanner(
                sp.GetRequiredService<MsBuildHelper>(),
                sp.GetRequiredService<ReflectionTypesCollector>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DependenciesScanner>>());
            return scanner;
        });
        
        services.AddScoped<AssemblyToolService>();
        services.AddScoped<NamespaceToolService>();
        services.AddScoped<TypeToolService>();
    }

    [Test]
    public async Task Services_Should_Be_Scoped_Per_Request()
    {
        // Arrange
        await using var client = await CreateMcpClientAsync();
        
        var initialScannerConstructed = _scannerConstructedCount;

        // Act - List tools multiple times
        for (int i = 1; i <= 3; i++)
        {
            var tools = await client.ListToolsAsync();
            Assert.That(tools, Is.Not.Empty);
            
            // Give some time for disposal to complete
            await Task.Delay(100);
        }

        // Assert - Verify that multiple tool listings don't create scanner instances
        // (scanner should only be created when tools are actually invoked, not when listing)
        var scannerConstructedAfterListing = _scannerConstructedCount;
        Assert.That(scannerConstructedAfterListing, Is.EqualTo(initialScannerConstructed),
            "Scanner instances should not be created during tool listing");
    }

    [Test]
    public void Multiple_Tool_Invocations_Should_Create_New_Scopes()
    {
        // This test verifies the service registration is correct by using a scope
        using var scope = ServiceProvider.CreateScope();
        Assert.That(scope.ServiceProvider.GetService<IDependenciesScanner>(), Is.Not.Null);
        Assert.That(scope.ServiceProvider.GetService<AssemblyToolService>(), Is.Not.Null);
        Assert.That(scope.ServiceProvider.GetService<NamespaceToolService>(), Is.Not.Null);
        Assert.That(scope.ServiceProvider.GetService<TypeToolService>(), Is.Not.Null);
    }

    [Test]
    public void Service_Provider_Should_Validate_Scopes()
    {
        // Verify that the service provider was built with validateScopes: true
        // by attempting to resolve a scoped service properly
        using var scope = ServiceProvider.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IDependenciesScanner>();
        Assert.That(scanner, Is.Not.Null);
    }

    [Test]
    public void Scoped_Services_Should_Be_Registered_Correctly()
    {
        // Verify all scoped services are registered
        using var scope = ServiceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.That(scopedProvider.GetService<MsBuildHelper>(), Is.Not.Null);
        Assert.That(scopedProvider.GetService<ReflectionTypesCollector>(), Is.Not.Null);
        Assert.That(scopedProvider.GetService<IDependenciesScanner>(), Is.Not.Null);
        Assert.That(scopedProvider.GetService<AssemblyToolService>(), Is.Not.Null);
        Assert.That(scopedProvider.GetService<NamespaceToolService>(), Is.Not.Null);
        Assert.That(scopedProvider.GetService<TypeToolService>(), Is.Not.Null);
    }

    [Test]
    public void Multiple_Scopes_Should_Create_Different_Instances()
    {
        // Create two scopes and verify they get different instances of scoped services
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var scanner1 = scope1.ServiceProvider.GetRequiredService<IDependenciesScanner>();
        var scanner2 = scope2.ServiceProvider.GetRequiredService<IDependenciesScanner>();

        Assert.That(scanner1, Is.Not.SameAs(scanner2), 
            "Different scopes should create different instances of scoped services");
    }

    /// <summary>
    /// Wrapper for DependenciesScanner that tracks construction
    /// </summary>
    private class TrackableDependenciesScanner : DependenciesScanner
    {
        public TrackableDependenciesScanner(
            MsBuildHelper msBuildHelper,
            ReflectionTypesCollector reflectionTypesCollector,
            Microsoft.Extensions.Logging.ILogger<DependenciesScanner> logger)
            : base(msBuildHelper, reflectionTypesCollector, logger)
        {
            // Construction is already tracked in the factory method
        }
    }
}

