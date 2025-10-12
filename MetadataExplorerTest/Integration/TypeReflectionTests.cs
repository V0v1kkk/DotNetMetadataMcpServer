using DotNetMetadataMcpServer;
using DotNetMetadataMcpServer.Services;

namespace MetadataExplorerTest.Integration;

[TestFixture]
[Parallelizable(ParallelScope.Self)] // Uses OneTimeSetUp with shared scanner
public class TypeReflectionTests
{
    private DependenciesScanner _scanner;
    private TypeToolService _service;
    private string _testProjectPath;

    [OneTimeSetUp]
    public void Setup()
    {
        var testDirectory = TestContext.CurrentContext.TestDirectory;
        _testProjectPath = Path.GetFullPath(Path.Combine(testDirectory, "../../../../DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj"));
        _scanner = new DependenciesScanner(new MsBuildHelper(), new ReflectionTypesCollector());
        _service = new TypeToolService(_scanner);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _scanner.Dispose();
    }

    [Test]
    public void GetTypes_ForSimpleTypeInfo_VerifyFormatting()
    {
        var response = _service.GetTypes(_testProjectPath, new List<string>(), new List<string>(), 1, 100);
        
        var typeInfo = response.TypeData.FirstOrDefault(t => t.FullName.EndsWith("SimpleTypeInfo"));
        Assert.That(typeInfo, Is.Not.Null, "SimpleTypeInfo type should be found");
        
        // Verify that properties have correct formatting with accessors
        Assert.That(typeInfo.Properties, Is.Not.Null);
        Assert.That(typeInfo.Properties, Is.Not.Empty, "SimpleTypeInfo should have properties");
        Assert.That(typeInfo.Properties, Has.All.Matches<string>(p => 
            p.Contains("{ get; set; }") || 
            p.Contains("{ get; }") ||
            p.Contains("{ get; init; }")
        ));
        
        // Verify property format matches pattern: [modifiers] Type Name { accessor; }
        Assert.That(typeInfo.Properties, Has.All.Match(
            @"^(\w+\s+)*[\w\.]+(\<[\w\.,\s<>]+\>)?\s+\w+\s*{.*}$"));
        
        // Find FullName property
        var fullNameProp = typeInfo.Properties
            .FirstOrDefault(p => p.Contains("FullName"));
        
        // Verify FullName property existence and format
        Assert.Multiple(() =>
        {
            Assert.That(fullNameProp, Is.Not.Null, "Should have FullName property");
            Assert.That(fullNameProp, Contains.Substring("System.String"));
            Assert.That(fullNameProp, Contains.Substring("{ get; init; }"));
            Assert.That(fullNameProp, Contains.Substring("required"));
        });
    }

    [Test]
    public void GetTypes_ForTypeWithInterfaces_IncludesImplementsList()
    {
        var response = _service.GetTypes(_testProjectPath, new List<string>(), new List<string>(), 1, 100);
        
        // Find a type that implements interfaces (e.g., IDisposable)
        var typeWithInterfaces = response.TypeData.FirstOrDefault(t => 
            t.Implements != null && t.Implements.Any());
            
        Assert.That(typeWithInterfaces, Is.Not.Null, "Should find at least one type implementing interfaces");
        Assert.That(typeWithInterfaces.Implements, Is.Not.Empty);
    }
}
