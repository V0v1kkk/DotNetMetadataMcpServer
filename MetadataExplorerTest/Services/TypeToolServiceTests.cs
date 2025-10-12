using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Models;
using DotNetMetadataMcpServer.Services;

namespace MetadataExplorerTest.Services;

[TestFixture]
[NonParallelizable] // Uses DependenciesScanner which calls MSBuildLocator.RegisterDefaults()
public class TypeToolServiceTests
{
    private string _testProjectPath;
    private DependenciesScanner _scanner;
    private TypeToolService _service;

    [SetUp]
    public void Setup()
    {
        var testDirectory = TestContext.CurrentContext.TestDirectory;
        var relativePath = Path.Combine(testDirectory, "../../../../DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj");
        _testProjectPath = Path.GetFullPath(relativePath);
        _scanner = new DependenciesScanner(new MsBuildHelper(), new ReflectionTypesCollector());
        _service = new TypeToolService(_scanner);
    }

    [TearDown]
    public void TearDown()
    {
        _scanner.Dispose();
    }

    [Test]
    public void GetTypes_WithAllowedNamespaces_ReturnsFilteredResults()
    {
        const string allowedNamespace = "DotNetMetadataMcpServer.Models";
        
        var allowedNamespaces = new List<string> { allowedNamespace };
        var response = _service.GetTypes(_testProjectPath, allowedNamespaces, new List<string>(), 1, 20);
        
        Assert.That(response.TypeData, Is.Not.Empty);
        Assert.That(response.TypeData, Is.All.Matches<SimpleTypeInfo>(t => 
            t.FullName.StartsWith(allowedNamespace)));
    }

    [Test]
    public void GetTypes_WithFilters_ReturnsFilteredResults()
    {
        const string filter = "*Parameters";
        
        var filters = new List<string> { filter };
        var response = _service.GetTypes(_testProjectPath, new List<string>(), filters, 1, 20);
        
        Assert.That(response.TypeData, Is.Not.Empty);
        Assert.That(response.TypeData, Is.All.Matches<SimpleTypeInfo>(t => 
            t.FullName.EndsWith("Parameters")));
    }

    [Test]
    public void GetTypes_Pagination_ReturnsCorrectPage()
    {
        const int pageSize = 5;
        
        var response1 = _service.GetTypes(_testProjectPath, new List<string>(), new List<string>(), 1, pageSize);
        var response2 = _service.GetTypes(_testProjectPath, new List<string>(), new List<string>(), 2, pageSize);
        
        Assert.That(response1.TypeData, Is.Not.Empty);
        Assert.That(response2.TypeData, Is.Not.Empty);
        
        Assert.That(response1.TypeData, Is.Not.EqualTo(response2.TypeData));
        Assert.That(response1.TypeData, Is.Not.EquivalentTo(response2.TypeData));
        
        Assert.That(response1.CurrentPage, Is.EqualTo(1));
        Assert.That(response2.CurrentPage, Is.EqualTo(2));
        
        Assert.That(response1.AvailablePages, Is.EquivalentTo(response2.AvailablePages));
    }
    
    [Test]
    public void GetTypes_WithInvalidPageNumber_ReturnsEmptyResults()
    {
        const int invalidPageNumber = 10000;
        
        var response = _service.GetTypes(_testProjectPath, new List<string>(), new List<string>(), invalidPageNumber, 20);
        
        Assert.That(response.TypeData, Is.Empty);
        Assert.That(response.CurrentPage, Is.EqualTo(invalidPageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
    }
}
