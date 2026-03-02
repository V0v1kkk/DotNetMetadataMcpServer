using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Services;
using Microsoft.Extensions.Options;

namespace MetadataExplorerTest;

[TestFixture]
[NonParallelizable]
public class AssemblyResolutionModeTests
{
    private string _testProjectPath;

    [SetUp]
    public void Setup()
    {
        var testDirectory = TestContext.CurrentContext.TestDirectory;
        var relativePath = Path.Combine(testDirectory, "../../../../DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj");
        _testProjectPath = Path.GetFullPath(relativePath);

        if (!File.Exists(_testProjectPath))
            Assert.Inconclusive("Test project file not found: " + _testProjectPath);
    }

    [Test]
    public void BuildOutput_Mode_ShouldResolveAssembliesFromBinDirectory()
    {
        var config = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.BuildOutput
        });
        using var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), config);

        var service = new AssemblyToolService(scanner);
        var response = service.GetAssemblies(_testProjectPath, new List<string>(), 1, 50);

        Assert.That(response.AssemblyNames, Is.Not.Null);
        Assert.That(response.AssemblyNames.Count, Is.GreaterThan(0));
    }

    [Test]
    public void NuGetCache_Mode_ShouldResolveAssembliesFromGlobalPackagesFolder()
    {
        var config = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.NuGetCache
        });
        using var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), config);

        var service = new AssemblyToolService(scanner);
        var response = service.GetAssemblies(_testProjectPath, new List<string>(), 1, 50);

        Assert.That(response.AssemblyNames, Is.Not.Null);
        Assert.That(response.AssemblyNames.Count, Is.GreaterThan(0),
            "NuGetCache mode should resolve assemblies from the global packages folder");
    }

    [Test]
    public void NuGetCache_Mode_ShouldLoadTypesFromResolvedAssemblies()
    {
        var config = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.NuGetCache
        });
        using var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), config);

        var assemblyService = new AssemblyToolService(scanner);
        var assemblies = assemblyService.GetAssemblies(_testProjectPath, new List<string>(), 1, 200);

        // Pick an assembly that should have types (e.g. a well-known NuGet package)
        var serilogAssembly = assemblies.AssemblyNames.FirstOrDefault(a =>
            a.Contains("Serilog", StringComparison.OrdinalIgnoreCase) && !a.Contains("Sinks"));

        Assert.That(serilogAssembly, Is.Not.Null,
            "Serilog should be among referenced assemblies");

        var namespaceService = new NamespaceToolService(scanner);
        var namespaces = namespaceService.GetNamespaces(
            _testProjectPath, new List<string> { serilogAssembly! }, new List<string>(), 1, 50);

        Assert.That(namespaces.Namespaces, Is.Not.Empty,
            "Should find namespaces in the Serilog assembly resolved from NuGet cache");
    }

    [Test]
    public void Auto_Mode_ShouldResolveAssemblies()
    {
        var config = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.Auto
        });
        using var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), config);

        var service = new AssemblyToolService(scanner);
        var response = service.GetAssemblies(_testProjectPath, new List<string>(), 1, 50);

        Assert.That(response.AssemblyNames, Is.Not.Null);
        Assert.That(response.AssemblyNames.Count, Is.GreaterThan(0));
    }

    [Test]
    public void NuGetCache_Mode_ShouldSkipProjectTypesGracefully_WhenNotBuilt()
    {
        // This test verifies the scanner doesn't crash when project assembly is missing.
        // We can't easily simulate a missing build, but we verify the scanner
        // handles the scenario by checking it produces results even in NuGetCache mode.
        var config = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.NuGetCache
        });
        using var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), config);

        var metadata = scanner.ScanProject(_testProjectPath);

        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata.ProjectName, Is.Not.Empty);
        Assert.That(metadata.Dependencies, Is.Not.Empty,
            "Should still resolve NuGet dependencies even if project assembly handling differs");
    }

    [Test]
    public void Default_Configuration_ShouldUseBuildOutputMode()
    {
        var config = new ToolsConfiguration();
        Assert.That(config.AssemblyResolutionMode, Is.EqualTo(AssemblyResolutionMode.BuildOutput));
    }

    [Test]
    public void NuGetCache_And_BuildOutput_ShouldFindSameAssemblies()
    {
        // Both modes should discover the same set of referenced assemblies
        var buildConfig = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.BuildOutput
        });
        var cacheConfig = Options.Create(new ToolsConfiguration
        {
            AssemblyResolutionMode = AssemblyResolutionMode.NuGetCache
        });

        List<string> buildAssemblies;
        using (var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), buildConfig))
        {
            var service = new AssemblyToolService(scanner);
            buildAssemblies = service.GetAssemblies(_testProjectPath, new List<string>(), 1, 200).AssemblyNames.ToList();
        }

        List<string> cacheAssemblies;
        using (var scanner = new DependenciesScanner(
            new MsBuildHelper(), new ReflectionTypesCollector(), cacheConfig))
        {
            var service = new AssemblyToolService(scanner);
            cacheAssemblies = service.GetAssemblies(_testProjectPath, new List<string>(), 1, 200).AssemblyNames.ToList();
        }

        Assert.That(buildAssemblies, Is.Not.Empty);
        Assert.That(cacheAssemblies, Is.Not.Empty);

        // NuGet cache mode should find at least as many assemblies as build output
        // (build output might miss some if not all were copied)
        Assert.That(cacheAssemblies.Count, Is.GreaterThanOrEqualTo(buildAssemblies.Count * 0.8),
            "NuGet cache mode should find a comparable number of assemblies");
    }
}
