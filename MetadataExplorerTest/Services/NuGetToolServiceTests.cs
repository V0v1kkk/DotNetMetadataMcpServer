using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MetadataExplorerTest.Services;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NuGetToolServiceTests
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
    public async Task SearchPackagesAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        const string searchQuery = "Newtonsoft.Json";
        var filters = new List<string>();
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.SearchPackagesAsync(searchQuery, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Packages, Is.Not.Empty);
        Assert.That(response.CurrentPage, Is.EqualTo(pageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
        
        // At least one package should contain the search query in its ID
        Assert.That(response.Packages, Has.Some.Matches<DotNetMetadataMcpServer.Models.NuGetPackageInfo>(
            p => p.Id.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public async Task SearchPackagesAsync_WithFilter_ReturnsFilteredResults()
    {
        // Arrange
        const string searchQuery = "Json";
        var filters = new List<string> { "Newtonsoft*" };
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.SearchPackagesAsync(searchQuery, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Packages, Is.Not.Empty);
        
        // All packages should match the filter
        Assert.That(response.Packages, Is.All.Matches<DotNetMetadataMcpServer.Models.NuGetPackageInfo>(
            p => p.Id.StartsWith("Newtonsoft", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public async Task GetPackageVersionsAsync_WithValidPackageId_ReturnsVersions()
    {
        // Arrange
        const string packageId = "Newtonsoft.Json";
        var filters = new List<string>();
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.GetPackageVersionsAsync(packageId, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Versions, Is.Not.Empty);
        Assert.That(response.PackageId, Is.EqualTo(packageId));
        Assert.That(response.CurrentPage, Is.EqualTo(pageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
        
        // All versions should be for the requested package
        Assert.That(response.Versions, Is.All.Matches<DotNetMetadataMcpServer.Models.NuGetPackageInfo>(
            v => v.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)));
        
        // Note: Not all package versions may have dependency information
        // so we don't assert on dependency groups
    }

    [Test]
    public async Task GetPackageVersionsAsync_WithVersionFilter_ReturnsFilteredVersions()
    {
        // Arrange
        const string packageId = "Newtonsoft.Json";
        var filters = new List<string> { "13.*" }; // Filter for version 13.x
        const bool includePrerelease = false;
        const int pageNumber = 1;
        const int pageSize = 10;

        // Act
        var response = await _service.GetPackageVersionsAsync(packageId, filters, includePrerelease, pageNumber, pageSize);

        // Assert
        Assert.That(response.Versions, Is.Not.Empty);
        
        // All versions should match the filter
        Assert.That(response.Versions, Is.All.Matches<DotNetMetadataMcpServer.Models.NuGetPackageInfo>(
            v => v.Version.StartsWith("13.", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public async Task SearchPackagesAsync_WithInvalidPageNumber_ReturnsEmptyResults()
    {
        // Arrange
        const string searchQuery = "Newtonsoft.Json";
        var filters = new List<string>();
        const bool includePrerelease = false;
        const int invalidPageNumber = 10000;
        const int pageSize = 10;

        // Act
        var response = await _service.SearchPackagesAsync(searchQuery, filters, includePrerelease, invalidPageNumber, pageSize);

        // Assert
        Assert.That(response.Packages, Is.Empty);
        Assert.That(response.CurrentPage, Is.EqualTo(invalidPageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
    }

    [Test]
    public async Task GetPackageVersionsAsync_WithInvalidPageNumber_ReturnsEmptyResults()
    {
        // Arrange
        const string packageId = "Newtonsoft.Json";
        var filters = new List<string>();
        const bool includePrerelease = false;
        const int invalidPageNumber = 10000;
        const int pageSize = 10;

        // Act
        var response = await _service.GetPackageVersionsAsync(packageId, filters, includePrerelease, invalidPageNumber, pageSize);

        // Assert
        Assert.That(response.Versions, Is.Empty);
        Assert.That(response.CurrentPage, Is.EqualTo(invalidPageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
    }
}