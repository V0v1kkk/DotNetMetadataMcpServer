using System.Reflection;
using System.Runtime.Loader;
using DependencyGraph.Core.Graph;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.ProjectModel;

namespace DotNetMetadataMcpServer;

public class DependenciesScanner : IDependenciesScanner
{
    private readonly MsBuildHelper _msbuild;
    private readonly ReflectionTypesCollector _reflection;
    private readonly ILogger _nuGetLogger;
    private readonly ILogger<DependenciesScanner> _logger;

    private readonly HashSet<IDependencyGraphNode> _visitedNodes = new();

    private string _baseDir = "";

    public DependenciesScanner(
        MsBuildHelper msBuildHelper,
        ReflectionTypesCollector reflectionTypesCollector,
        ILogger<DependenciesScanner>? logger = null,
        ILogger<LockFileFormat>? nuGetLogger = null)
    {
        _msbuild = msBuildHelper;
        _reflection = reflectionTypesCollector;
        _nuGetLogger = nuGetLogger ?? NullLogger<LockFileFormat>.Instance;
        _logger = logger ?? NullLogger<DependenciesScanner>.Instance;
        
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }

    /// <summary>
    /// Scans .csproj:
    /// 1. MSBuild → assemblyPath, assetsFilePath
    /// 2. Loads public types from the project itself (assemblyPath)
    /// 3. Parses project.assets.json, builds DependencyGraph
    /// 4. Loads assemblies for packages (via .RuntimeAssemblies)
    /// 5. Returns ProjectMetadata
    /// </summary>
    public ProjectMetadata ScanProject(string csprojPath)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var (asmPath, assetsPath, tfm) = _msbuild.EvaluateProject(csprojPath);
        
        _baseDir = Path.GetDirectoryName(asmPath) ?? "";

        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        var pm = new ProjectMetadata
        {
            ProjectName = projectName,
            TargetFramework = tfm,
            AssemblyPath = asmPath
        };
        var depList = new List<DependencyInfo>();
        pm.Dependencies = depList;

        // 1) Load public types from the project itself
        var projectTypes = _reflection.LoadAssemblyTypes(asmPath);
        pm.ProjectTypes.AddRange(projectTypes);

        // 2) If there is no assetsFile, skip dependencies
        if (string.IsNullOrEmpty(assetsPath) || !File.Exists(assetsPath))
        {
            _logger.LogWarning("No project.assets.json found. Skip dependency scanning.");
            return pm;
        }

        // 3) Build DependencyGraph
        var lockFileFormat = new LockFileFormat();
        var lockFile = lockFileFormat.Read(assetsPath, new MicrosoftLoggerAdapter(_nuGetLogger));
        
        
        var theFirstTarget = lockFile.Targets.FirstOrDefault();
        if (theFirstTarget == null)
        {
            _logger.LogWarning("No targets found in lock file.");
            return pm;
        }
        
        foreach (var lib in theFirstTarget.Libraries)
        {
            var d = BuildDependencyInfo(lib);
            depList.AddRange(d);
        }
        

        /*var depGraphFactory = new DependencyGraphFactory(new DependencyGraphFactoryOptions
        {
            Excludes = ["Microsoft.*", "System.*"]
        });
        
        var graph = depGraphFactory.FromLockFile(lockFile);

        var rootNode = graph.RootNodes.FirstOrDefault() as RootProjectDependencyGraphNode;
        if (rootNode == null)
        {
            _logger.LogWarning("No RootProjectDependencyGraphNode found.");
            return pm;
        }

        var tfmNode = rootNode.Dependencies.OfType<TargetFrameworkDependencyGraphNode>().FirstOrDefault();
        if (tfmNode == null)
        {
            _logger.LogWarning("No TargetFrameworkDependencyGraphNode found under root.");
            return pm;
        }
        
        foreach (var child in tfmNode.Dependencies)
        {
            var d = BuildDependencyInfo(child);
            if (d != null) depList.Add(d);
        }*/
        

        return pm;
    }

    private List<DependencyInfo> BuildDependencyInfo(LockFileTargetLibrary lockFileTargetLibrary)
    {
        var result = new List<DependencyInfo>();
        foreach (var lockFileItem in lockFileTargetLibrary.RuntimeAssemblies)
        {
            var rel = lockFileItem.Path; // e.g., "lib/net9.0/FluentValidation.dll"
            var fileName = Path.GetFileName(rel);
            var full = Path.Combine(_baseDir, fileName);
            var types = _reflection.LoadAssemblyTypes(full);
            var info = new DependencyInfo
            {
                Name = lockFileTargetLibrary.Name ?? "Unknown",
                Version = lockFileTargetLibrary.Version?.ToNormalizedString() ?? "",
                NodeType = "package",
                Types = types
            };
            result.Add(info);
        }

        return result;
    }
    
    

    private DependencyInfo? BuildDependencyInfo(IDependencyGraphNode node)
    {
        // Check if already visited
        if (!_visitedNodes.Add(node))
            return null;

        switch (node)
        {
            case RootProjectDependencyGraphNode rootNode:
            {
                var info = new DependencyInfo
                {
                    Name = rootNode.Name,
                    NodeType = "root"
                };
                foreach (var child in rootNode.Dependencies)
                {
                    var c = BuildDependencyInfo(child);
                    if (c != null) info.Children.Add(c);
                }
                return info;
            }
            case TargetFrameworkDependencyGraphNode tfmNode:
            {
                var info = new DependencyInfo
                {
                    Name = tfmNode.ProjectName,
                    Version = tfmNode.TargetFrameworkIdentifier,
                    NodeType = "target framework dependency"
                };
                foreach (var child in tfmNode.Dependencies)
                {
                    var c = BuildDependencyInfo(child);
                    if (c != null) info.Children.Add(c);
                }
                return info;
            }
            case PackageDependencyGraphNode pkgNode:
            {
                var info = new DependencyInfo
                {
                    Name = pkgNode.Name,
                    Version = pkgNode.Version.ToNormalizedString(),
                    NodeType = "package"
                };
                // Load RuntimeAssemblies
                if (pkgNode.TargetLibrary != null)
                {
                    foreach (var asmItem in pkgNode.TargetLibrary.RuntimeAssemblies)
                    {
                        var rel = asmItem.Path; // e.g., "lib/net9.0/FluentValidation.dll"
                        var fileName = Path.GetFileName(rel);
                        var full = Path.Combine(_baseDir, fileName);
                        var types = _reflection.LoadAssemblyTypes(full);
                        info.Types.AddRange(types);
                    }
                }
                foreach (var child in pkgNode.Dependencies)
                {
                    var c = BuildDependencyInfo(child);
                    if (c != null) info.Children.Add(c);
                }
                return info;
            }
            case ProjectDependencyGraphNode pnode:
            {
                // Currently unable to load assemblies of other projects
                var info = new DependencyInfo
                {
                    Name = pnode.Name,
                    NodeType = "project"
                };
                foreach (var child in pnode.Dependencies)
                {
                    var c = BuildDependencyInfo(child);
                    if (c != null) info.Children.Add(c);
                }
                return info;
            }
            default:
            {
                var info = new DependencyInfo
                {
                    Name = node.ToString() ?? "Unknown",
                    NodeType = "unknown"
                };
                foreach (var child in node.Dependencies)
                {
                    var c = BuildDependencyInfo(child);
                    if (c != null) info.Children.Add(c);
                }
                return info;
            }
        }
    }
    
    private Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);
        var requestingAssemblyLocation = args.RequestingAssembly?.Location;
        string? baseDirectory = null;

        if (!string.IsNullOrEmpty(requestingAssemblyLocation))
        {
            baseDirectory = Path.GetDirectoryName(requestingAssemblyLocation);
        }

        if (string.IsNullOrEmpty(baseDirectory))
        {
            // In single-file deployments Assembly.Location returns empty; fallback to app base directory
            baseDirectory = AppContext.BaseDirectory;
        }

        var assemblyPath = Path.Combine(baseDirectory!, $"{assemblyName.Name}.dll");

        if (File.Exists(assemblyPath))
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
    }
}