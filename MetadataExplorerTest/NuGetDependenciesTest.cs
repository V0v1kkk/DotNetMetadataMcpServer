using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Models;
using DotNetMetadataMcpServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MetadataExplorerTest;

[TestFixture]
public class NuGetDependenciesTest
{
    private NuGetToolService _service;
    private Mock<ILogger<NuGetToolService>> _loggerMock;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<NuGetToolService>>();
        var config = Options.Create(new ToolsConfiguration());
        _service = new NuGetToolService(_loggerMock.Object, config);
    }

    [Test]
    public async Task GetSpecificPackageVersion_ShouldReturnCorrectDependencies()
    {
        // Arrange - Use a specific version of a package with known dependencies
        const string packageId = "Microsoft.EntityFrameworkCore";
        const string specificVersion = "7.0.0"; // Using a specific version for deterministic testing
        var filters = new List<string> { specificVersion };
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.GetPackageVersionsAsync(packageId, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Versions, Is.Not.Empty, "Should return package versions");
        Assert.That(response.PackageId, Is.EqualTo(packageId), "Package ID should match");
        
        // Find the specific version
        var versionInfo = response.Versions.FirstOrDefault(v => v.Version == specificVersion);
        Assert.That(versionInfo, Is.Not.Null, $"Version {specificVersion} should be found");
        
        // Verify dependency groups
        Assert.That(versionInfo.DependencyGroups, Is.Not.Empty, "Should have dependency groups");
        
        // Verify .NET 6.0 target framework dependencies
        var netCoreGroup = versionInfo.DependencyGroups.FirstOrDefault(g => g.TargetFramework.Contains("net6.0"));
        if (netCoreGroup != null)
        {
            Assert.That(netCoreGroup.Dependencies, Is.Not.Empty, "Should have dependencies for .NET 6.0");
            
            // Verify specific dependencies that should be present
            Assert.That(netCoreGroup.Dependencies, Has.Some.Matches<NuGetPackageDependency>(
                d => d.Id == "Microsoft.Extensions.Caching.Memory"),
                "Should have Microsoft.Extensions.Caching.Memory dependency");
                
            Assert.That(netCoreGroup.Dependencies, Has.Some.Matches<NuGetPackageDependency>(
                d => d.Id == "Microsoft.Extensions.DependencyInjection"),
                "Should have Microsoft.Extensions.DependencyInjection dependency");
        }
    }
    
    [Test]
    public async Task GetSpecificPackageWithMultipleFrameworks_ShouldHaveCorrectFrameworkTargets()
    {
        // Arrange - Use a specific version of a package known to target multiple frameworks
        const string packageId = "Newtonsoft.Json";
        const string specificVersion = "13.0.1"; // Using a specific version for deterministic testing
        var filters = new List<string> { specificVersion };
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.GetPackageVersionsAsync(packageId, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Versions, Is.Not.Empty, "Should return package versions");
        
        // Find the specific version
        var versionInfo = response.Versions.FirstOrDefault(v => v.Version == specificVersion);
        Assert.That(versionInfo, Is.Not.Null, $"Version {specificVersion} should be found");
        
        // Newtonsoft.Json 13.0.1 targets multiple frameworks
        Assert.That(versionInfo.DependencyGroups.Count, Is.GreaterThanOrEqualTo(2),
            "Should target at least 2 different frameworks");
            
        // Check for specific framework targets
        var frameworkTargets = versionInfo.DependencyGroups.Select(g => g.TargetFramework).ToList();
        Assert.That(frameworkTargets, Has.Some.Contains(".NETFramework,Version=v4.5"), "Should target .NET Framework 4.5");
        Assert.That(frameworkTargets, Has.Some.Contains(".NETStandard,Version=v2.0"), "Should target .NET Standard 2.0");
    }
    
    [Test]
    public async Task SearchPackages_WithSpecificQuery_ShouldReturnRelevantResults()
    {
        // Arrange
        const string searchQuery = "EntityFrameworkCore";
        var filters = new List<string>();
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.SearchPackagesAsync(searchQuery, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Packages, Is.Not.Empty, "Should return packages");
        
        // Verify that results are relevant to the search query
        Assert.That(response.Packages, Has.All.Matches<NuGetPackageInfo>(
            p => p.Id.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
                 (p.Description != null && p.Description.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))),
            "All results should be relevant to the search query");
            
        // Verify that Microsoft.EntityFrameworkCore is in the results
        Assert.That(response.Packages, Has.Some.Matches<NuGetPackageInfo>(
            p => p.Id == "Microsoft.EntityFrameworkCore"),
            "Microsoft.EntityFrameworkCore should be in the results");
    }
    
    [Test]
    public async Task SearchPackages_WithFiltering_ShouldReturnFilteredResults()
    {
        // Arrange
        const string searchQuery = "Json";
        var filters = new List<string> { "Newtonsoft*" }; // Only packages with Newtonsoft in the name
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.SearchPackagesAsync(searchQuery, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Packages, Is.Not.Empty, "Should return packages");
        
        // Verify that all results start with "Newtonsoft"
        Assert.That(response.Packages, Has.All.Matches<NuGetPackageInfo>(
            p => p.Id.StartsWith("Newtonsoft", StringComparison.OrdinalIgnoreCase)),
            "All results should start with 'Newtonsoft'");
            
        // Verify that Newtonsoft.Json is in the results
        Assert.That(response.Packages, Has.Some.Matches<NuGetPackageInfo>(
            p => p.Id == "Newtonsoft.Json"),
            "Newtonsoft.Json should be in the results");
    }
}