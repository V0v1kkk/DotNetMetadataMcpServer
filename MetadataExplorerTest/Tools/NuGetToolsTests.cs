using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Models;
using DotNetMetadataMcpServer.Services;
using DotNetMetadataMcpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MetadataExplorerTest.Tools;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NuGetToolsTests
{
    private NuGetToolService _nuGetToolService = null!;
    private IOptions<ToolsConfiguration> _toolsConfiguration = null!;
    private ILogger<NuGetTools> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _toolsConfiguration = Options.Create(new ToolsConfiguration
        {
            DefaultPageSize = 20,
            IntendResponse = false
        });
        _nuGetToolService = new NuGetToolService(NullLogger<NuGetToolService>.Instance, _toolsConfiguration);
        _logger = NullLogger<NuGetTools>.Instance;
    }

    [Test]
    public async Task SearchPackages_WithValidQuery_ReturnsResults()
    {
        // Act
        var resultJson = await NuGetTools.SearchPackages(
            _nuGetToolService,
            _toolsConfiguration,
            _logger,
            searchQuery: "Newtonsoft.Json",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: null,
            pageNumber: 1);

        // Assert
        Assert.That(resultJson, Is.Not.Null);
        Assert.That(resultJson, Is.Not.Empty);
        
        var response = JsonSerializer.Deserialize<NuGetPackageSearchResponse>(resultJson);
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Packages, Is.Not.Empty);
        Assert.That(response.Packages.Any(p => p.Id.Contains("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task SearchPackages_WithFilter_ReturnsFilteredResults()
    {
        // Act
        var resultJson = await NuGetTools.SearchPackages(
            _nuGetToolService,
            _toolsConfiguration,
            _logger,
            searchQuery: "json",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: new List<string> { "*Newtonsoft*" },
            pageNumber: 1);

        // Assert
        Assert.That(resultJson, Is.Not.Null);
        
        var response = JsonSerializer.Deserialize<NuGetPackageSearchResponse>(resultJson);
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Packages, Is.Not.Empty);
        
        // All results should contain "Newtonsoft" due to filter
        foreach (var package in response.Packages)
        {
            Assert.That(
                package.Id.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase) || 
                (package.Description?.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase) ?? false),
                Is.True,
                $"Package {package.Id} doesn't match filter *Newtonsoft*");
        }
    }

    [Test]
    public async Task GetPackageVersions_WithValidPackageId_ReturnsVersions()
    {
        // Act
        var resultJson = await NuGetTools.GetPackageVersions(
            _nuGetToolService,
            _toolsConfiguration,
            _logger,
            packageId: "Newtonsoft.Json",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: null,
            pageNumber: 1);

        // Assert
        Assert.That(resultJson, Is.Not.Null);
        
        var response = JsonSerializer.Deserialize<NuGetPackageVersionsResponse>(resultJson);
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.PackageId, Is.EqualTo("Newtonsoft.Json"));
        Assert.That(response.Versions, Is.Not.Empty);
        
        // Verify versions are properly formatted
        foreach (var version in response.Versions)
        {
            Assert.That(version.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(version.Id, Is.EqualTo("Newtonsoft.Json"));
        }
    }

    [Test]
    public async Task GetPackageVersions_WithVersionFilter_ReturnsFilteredVersions()
    {
        // Act
        var resultJson = await NuGetTools.GetPackageVersions(
            _nuGetToolService,
            _toolsConfiguration,
            _logger,
            packageId: "Newtonsoft.Json",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: new List<string> { "13.*" },
            pageNumber: 1);

        // Assert
        Assert.That(resultJson, Is.Not.Null);
        
        var response = JsonSerializer.Deserialize<NuGetPackageVersionsResponse>(resultJson);
        Assert.That(response, Is.Not.Null);
        
        // All versions should start with "13." due to filter
        foreach (var version in response.Versions)
        {
            Assert.That(version.Version.StartsWith("13."), Is.True,
                $"Version {version.Version} doesn't match filter 13.*");
        }
    }

    [Test]
    public async Task SearchPackages_WithIndentedResponse_ReturnsFormattedJson()
    {
        // Arrange
        var indentedConfig = Options.Create(new ToolsConfiguration
        {
            DefaultPageSize = 20,
            IntendResponse = true
        });

        // Act
        var resultJson = await NuGetTools.SearchPackages(
            _nuGetToolService,
            indentedConfig,
            _logger,
            searchQuery: "Newtonsoft.Json",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: null,
            pageNumber: 1);

        // Assert
        Assert.That(resultJson, Is.Not.Null);
        Assert.That(resultJson, Contains.Substring("\n")); // Indented JSON should contain newlines
        
        // Verify it's still valid JSON
        var response = JsonSerializer.Deserialize<NuGetPackageSearchResponse>(resultJson);
        Assert.That(response, Is.Not.Null);
    }

    [Test]
    public async Task GetPackageVersions_WithDependencies_ReturnsDependencyInfo()
    {
        // Act
        var resultJson = await NuGetTools.GetPackageVersions(
            _nuGetToolService,
            _toolsConfiguration,
            _logger,
            packageId: "Microsoft.Extensions.DependencyInjection",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: null,
            pageNumber: 1);

        // Assert
        Assert.That(resultJson, Is.Not.Null);
        
        var response = JsonSerializer.Deserialize<NuGetPackageVersionsResponse>(resultJson);
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Versions, Is.Not.Empty);
        
        // Find a version with dependencies
        var versionWithDeps = response.Versions.FirstOrDefault(v => v.DependencyGroups.Any());
        if (versionWithDeps != null)
        {
            Assert.That(versionWithDeps.DependencyGroups, Is.Not.Empty);
            
            foreach (var depGroup in versionWithDeps.DependencyGroups)
            {
                Assert.That(depGroup.TargetFramework, Is.Not.Null.And.Not.Empty);
                // Dependencies might be empty for some target frameworks
            }
        }
    }

    [Test]
    public async Task SearchPackages_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var smallPageConfig = Options.Create(new ToolsConfiguration
        {
            DefaultPageSize = 5,
            IntendResponse = false
        });

        // Act - Get first page
        var page1Json = await NuGetTools.SearchPackages(
            _nuGetToolService,
            smallPageConfig,
            _logger,
            searchQuery: "logging",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: null,
            pageNumber: 1);

        var page2Json = await NuGetTools.SearchPackages(
            _nuGetToolService,
            smallPageConfig,
            _logger,
            searchQuery: "logging",
            includePrerelease: false,
            fullTextFiltersWithWildCardSupport: null,
            pageNumber: 2);

        // Assert
        var page1 = JsonSerializer.Deserialize<NuGetPackageSearchResponse>(page1Json);
        var page2 = JsonSerializer.Deserialize<NuGetPackageSearchResponse>(page2Json);
        
        Assert.That(page1, Is.Not.Null);
        Assert.That(page2, Is.Not.Null);
        
        Assert.That(page1!.CurrentPage, Is.EqualTo(1));
        Assert.That(page2!.CurrentPage, Is.EqualTo(2));
        
        // Pages should have different packages (assuming there are more than 5 results)
        if (page1.Packages.Count > 0 && page2.Packages.Count > 0)
        {
            Assert.That(page1.Packages.First().Id, Is.Not.EqualTo(page2.Packages.First().Id));
        }
    }
}

