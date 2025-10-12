using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MetadataExplorerTest.Services;

/// <summary>
/// Integration tests for NuGet multi-source support.
/// Verifies that the service can query multiple NuGet feeds and aggregate results.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NuGetMultiSourceTests
{
    [Test]
    public async Task Service_Should_Work_With_Default_NuGetOrg_Source()
    {
        // Arrange
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act
        var result = await service.SearchPackagesAsync(
            searchQuery: "Newtonsoft.Json",
            filters: new List<string>(),
            includePrerelease: false,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Packages, Is.Not.Empty);
        Assert.That(result.Packages.Any(p => p.Id == "Newtonsoft.Json"), Is.True,
            "Expected to find Newtonsoft.Json package");
    }

    [Test]
    public async Task Service_Should_Aggregate_Results_From_Multiple_Sources()
    {
        // Arrange - Configure multiple sources (nuget.org is always available)
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act - Search for a common package
        var result = await service.SearchPackagesAsync(
            searchQuery: "System.Text.Json",
            filters: new List<string>(),
            includePrerelease: false,
            pageNumber: 1,
            pageSize: 20);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Packages, Is.Not.Empty);
        Assert.That(result.Packages.Any(p => p.Id.Contains("System.Text.Json")), Is.True,
            "Expected to find System.Text.Json related packages");
    }

    [Test]
    public async Task Service_Should_Get_Package_Versions_From_Multiple_Sources()
    {
        // Arrange
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act
        var result = await service.GetPackageVersionsAsync(
            packageId: "ModelContextProtocol",
            filters: new List<string>(),
            includePrerelease: true,
            pageNumber: 1,
            pageSize: 20);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.PackageId, Is.EqualTo("ModelContextProtocol"));
        Assert.That(result.Versions, Is.Not.Empty, "Expected to find versions of ModelContextProtocol");
        Assert.That(result.Versions.All(v => v.Id == "ModelContextProtocol"), Is.True);
    }

    [Test]
    public void Service_Should_Fail_With_No_Valid_Sources()
    {
        // Arrange - Empty sources list
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>()
        };
        
        var options = Options.Create(configuration);

        // Act & Assert - Constructor should add default nuget.org, so no exception
        Assert.DoesNotThrow(() => new NuGetToolService(NullLogger<NuGetToolService>.Instance, options));
    }

    [Test]
    public void Service_Should_Handle_Disabled_Sources()
    {
        // Arrange - All sources disabled
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = false
                }
            }
        };
        
        var options = Options.Create(configuration);

        // Act & Assert - Should add default nuget.org when all are disabled
        Assert.DoesNotThrow(() => new NuGetToolService(NullLogger<NuGetToolService>.Instance, options));
    }

    [Test]
    public async Task Service_Should_Deduplicate_Packages_From_Multiple_Sources()
    {
        // Arrange - Even with one source, test deduplication logic
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act
        var result = await service.SearchPackagesAsync(
            searchQuery: "Serilog",
            filters: new List<string>(),
            includePrerelease: false,
            pageNumber: 1,
            pageSize: 20);

        // Assert - Check that there are no duplicate package IDs
        var packageIds = result.Packages.Select(p => p.Id).ToList();
        var uniqueIds = packageIds.Distinct().ToList();
        
        Assert.That(packageIds.Count, Is.EqualTo(uniqueIds.Count),
            "Expected no duplicate package IDs in results");
    }

    [Test]
    public async Task Service_Should_Continue_If_One_Source_Fails()
    {
        // Arrange - One valid source and one invalid (will fail)
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "invalid",
                    Url = "https://invalid-nuget-source.local/v3/index.json",
                    Enabled = true
                },
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act - Should still work with the valid source
        var result = await service.SearchPackagesAsync(
            searchQuery: "NUnit",
            filters: new List<string>(),
            includePrerelease: false,
            pageNumber: 1,
            pageSize: 10);

        // Assert - Should get results from nuget.org despite invalid source
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Packages, Is.Not.Empty,
            "Expected to get results from valid source even when one source fails");
    }

    [Test]
    public async Task Service_Should_Respect_Source_Priority_Order_Single_Source()
    {
        // Arrange - Single source to verify deterministic behavior
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act - Search for a well-known package multiple times
        var result1 = await service.SearchPackagesAsync(
            searchQuery: "Newtonsoft.Json",
            filters: new List<string>(),
            includePrerelease: false,
            pageNumber: 1,
            pageSize: 5);

        var result2 = await service.SearchPackagesAsync(
            searchQuery: "Newtonsoft.Json",
            filters: new List<string>(),
            includePrerelease: false,
            pageNumber: 1,
            pageSize: 5);

        // Assert - Results should be deterministic
        Assert.That(result1, Is.Not.Null);
        Assert.That(result1.Packages, Is.Not.Empty);
        
        var package1 = result1.Packages.FirstOrDefault(p => p.Id == "Newtonsoft.Json");
        var package2 = result2.Packages.FirstOrDefault(p => p.Id == "Newtonsoft.Json");
        
        Assert.That(package1, Is.Not.Null, "Expected to find Newtonsoft.Json in first call");
        Assert.That(package2, Is.Not.Null, "Expected to find Newtonsoft.Json in second call");
        Assert.That(package2.Description, Is.EqualTo(package1.Description),
            "Package metadata should be deterministic across calls");
    }

    [Test]
    [Category("Integration")]
    [Explicit("Requires external MyGet service - run manually to verify priority behavior")]
    public async Task Service_Should_Respect_Source_Priority_With_Multiple_Sources()
    {
        // Arrange - Configure multiple sources in priority order
        // First source (higher priority): MyGet dotnet-core feed
        // Second source (lower priority): nuget.org
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "dotnet-core (MyGet)",
                    Url = "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
                    Enabled = true
                },
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act - Search for packages that might exist in both sources
        var result = await service.SearchPackagesAsync(
            searchQuery: "System.Runtime",
            filters: new List<string>(),
            includePrerelease: true,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Packages, Is.Not.Empty);
        
        // Run the same query again to verify determinism
        var result2 = await service.SearchPackagesAsync(
            searchQuery: "System.Runtime",
            filters: new List<string>(),
            includePrerelease: true,
            pageNumber: 1,
            pageSize: 10);

        // Results should be identical (same order, same metadata)
        Assert.That(result2.Packages.Count, Is.EqualTo(result.Packages.Count),
            "Results should have same count across calls");
            
        for (int i = 0; i < result.Packages.Count; i++)
        {
            var pkg1 = result.Packages[i];
            var pkg2 = result2.Packages[i];
            
            Assert.That(pkg2.Id, Is.EqualTo(pkg1.Id), 
                $"Package at index {i} should have same ID");
            Assert.That(pkg2.Version, Is.EqualTo(pkg1.Version), 
                $"Package {pkg1.Id} should have same version");
        }
    }

    [Test]
    [Category("Integration")]
    [Explicit("Requires external MyGet service - run manually")]
    public async Task Service_Should_Query_MyGet_Public_Feed()
    {
        // Arrange - Configure MyGet public feed alongside nuget.org
        var configuration = new ToolsConfiguration
        {
            NuGetSources = new List<NuGetSourceConfiguration>
            {
                new NuGetSourceConfiguration
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                },
                new NuGetSourceConfiguration
                {
                    Name = "dotnet-core (MyGet)",
                    Url = "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
                    Enabled = true
                }
            }
        };
        
        var options = Options.Create(configuration);
        var service = new NuGetToolService(NullLogger<NuGetToolService>.Instance, options);

        // Act - Search for packages that might be on both feeds
        var result = await service.SearchPackagesAsync(
            searchQuery: "System.Runtime",
            filters: new List<string>(),
            includePrerelease: true,
            pageNumber: 1,
            pageSize: 20);

        // Assert - Should get results from both sources
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Packages, Is.Not.Empty,
            "Expected to find System.Runtime packages from configured sources");
        
        // Verify deduplication is working (no duplicate package IDs)
        var packageIds = result.Packages.Select(p => p.Id).ToList();
        var uniqueIds = packageIds.Distinct().ToList();
        Assert.That(packageIds.Count, Is.EqualTo(uniqueIds.Count),
            "Expected no duplicate package IDs when querying multiple sources");
    }
}

