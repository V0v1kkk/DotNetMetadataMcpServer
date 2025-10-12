using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Services;

namespace MetadataExplorerTest.Services;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NamespaceToolServiceTests
{
    private string _testProjectPath;
    private DependenciesScanner _scanner;
    private NamespaceToolService _service;

    [SetUp]
    public void Setup()
    {
        var testDirectory = TestContext.CurrentContext.TestDirectory;
        var relativePath = Path.Combine(testDirectory, "../../../../DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj");
        _testProjectPath = Path.GetFullPath(relativePath);
        _scanner = new DependenciesScanner(new MsBuildHelper(), new ReflectionTypesCollector());
        _service = new NamespaceToolService(_scanner);
    }

    [TearDown]
    public void TearDown()
    {
        _scanner.Dispose();
    }

    [Test]
    public void GetNamespaces_WithAllowedAssemblies_ReturnsFilteredResults()
    {
        const string allowedAssembly = "DotNetMetadataMcpServer";
        
        var allowedAssemblies = new List<string> { allowedAssembly };
        var response = _service.GetNamespaces(_testProjectPath, allowedAssemblies, [], 1, 20);
        
        Assert.That(response.Namespaces, Is.Not.Empty);
        Assert.That(response.Namespaces, Is.All.Matches<string>(ns => 
            ns.StartsWith(allowedAssembly)));
    }

    [Test]
    public void GetNamespaces_WithFilters_ReturnsFilteredResults()
    {
        const string filter  = "Helpers";
        const string filterWildCard = "*" + filter;
        
        var filters = new List<string> { filterWildCard };
        var response = _service.GetNamespaces(_testProjectPath, [], filters, 1, 20);
        
        Assert.That(response.Namespaces, Is.Not.Empty);
        Assert.That(response.Namespaces, Is.All.Contains(filter));
    }

    [Test]
    public void GetNamespaces_Pagination_ReturnsCorrectPage()
    {
        const int pageSize = 5;
        
        var response1 = _service.GetNamespaces(_testProjectPath, new List<string>(), [], 1, pageSize);
        var response2 = _service.GetNamespaces(_testProjectPath, new List<string>(), [], 2, pageSize);
        
        Assert.That(response1.Namespaces, Is.Not.Empty);
        Assert.That(response2.Namespaces, Is.Not.Empty);
        
        Assert.That(response1.Namespaces, Is.Not.EqualTo(response2.Namespaces));
        Assert.That(response1.Namespaces, Is.Not.EquivalentTo(response2.Namespaces));
        
        Assert.That(response1.CurrentPage, Is.EqualTo(1));
        Assert.That(response2.CurrentPage, Is.EqualTo(2));
        
        Assert.That(response1.AvailablePages, Is.EquivalentTo(response2.AvailablePages));
    }
    
    [Test]
    public void GetNamespaces_WithInvalidPageNumber_ReturnsEmptyResults()
    {
        const int invalidPageNumber = 10000;
        
        var response = _service.GetNamespaces(_testProjectPath, new List<string>(), [], invalidPageNumber, 20);
        
        Assert.That(response.Namespaces, Is.Empty);
        Assert.That(response.CurrentPage, Is.EqualTo(invalidPageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
    }
}
