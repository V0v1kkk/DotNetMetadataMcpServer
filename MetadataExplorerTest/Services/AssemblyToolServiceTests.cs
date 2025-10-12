using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Services;

namespace MetadataExplorerTest.Services;

[TestFixture]
[NonParallelizable] // Uses DependenciesScanner which calls MSBuildLocator.RegisterDefaults()
public class AssemblyToolServiceTests
{
    private string _testProjectPath;
    private DependenciesScanner _scanner;
    private AssemblyToolService _service;

    [SetUp]
    public void Setup()
    {
        var testDirectory = TestContext.CurrentContext.TestDirectory;
        var relativePath = Path.Combine(testDirectory, "../../../../DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj");
        _testProjectPath = Path.GetFullPath(relativePath);
        _scanner = new DependenciesScanner(new MsBuildHelper(), new ReflectionTypesCollector());
        _service = new AssemblyToolService(_scanner);
    }

    [TearDown]
    public void TearDown()
    {
        _scanner.Dispose();
    }

    [Test]
    public void GetAssemblies_WithValidFilters_ReturnsFilteredResults()
    {
        const string filter = "DotNetMetadataMcpServer";
        
        var filters = new List<string> { filter };
        var response = _service.GetAssemblies(_testProjectPath, filters, 1, 20);
        
        Assert.That(response.AssemblyNames, Is.Not.Empty);
        Assert.That(response.AssemblyNames, Is.All.Contains(filter));
    }

    [Test]
    public void GetAssemblies_Pagination_ReturnsCorrectPage()
    {
        const int pageSize = 5;
        
        var response1 = _service.GetAssemblies(_testProjectPath, [], 1, pageSize);
        var response2 = _service.GetAssemblies(_testProjectPath, [], 2, pageSize);
        
        Assert.That(response1.AssemblyNames, Is.Not.EqualTo(response2.AssemblyNames));
        Assert.That(response1.CurrentPage, Is.EqualTo(1));
        Assert.That(response2.CurrentPage, Is.EqualTo(2));
        Assert.That(response1.AvailablePages, Is.EquivalentTo(response2.AvailablePages));
    }
    
    [Test]
    public void GetAssemblies_WithInvalidPageNumber_ReturnsEmptyResults()
    {
        const int invalidPageNumber = 10000;
        
        var response = _service.GetAssemblies(_testProjectPath, [], invalidPageNumber, 20);
        
        Assert.That(response.AssemblyNames, Is.Empty);
        Assert.That(response.CurrentPage, Is.EqualTo(invalidPageNumber));
        Assert.That(response.AvailablePages, Is.Not.Empty);
    }
}
