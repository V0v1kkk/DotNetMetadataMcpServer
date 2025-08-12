using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetMetadataMcpServer;

public class MsBuildHelper
{
    private readonly ILogger<MsBuildHelper> _logger;

    public MsBuildHelper(ILogger<MsBuildHelper>? logger = null)
    {
        _logger = logger ?? NullLogger<MsBuildHelper>.Instance;
    }

    /// <summary>
    /// Loads .csproj via MSBuild, retrieves OutputPath, AssemblyName, TargetFramework.
    /// Searches for the compiled assembly (dll or exe), as well as project.assets.json.
    /// </summary>
    public (string assemblyPath, string assetsFilePath, string targetFramework)
        EvaluateProject(string csprojPath, string configuration = "Debug")
    {
        if (!File.Exists(csprojPath))
            throw new FileNotFoundException("CSProj not found", csprojPath);

        _logger.LogInformation("Loading project: {Proj}", csprojPath);
        
        using var projectCollection = new ProjectCollection();

        // Unload any existing projects with the same path
        var existingProject = projectCollection.LoadedProjects
            .FirstOrDefault(p => string.Equals(p.FullPath, csprojPath, StringComparison.OrdinalIgnoreCase));
        if (existingProject != null)
        {
            projectCollection.UnloadProject(existingProject);
        }

        var project = new Project(csprojPath, null, null, projectCollection);

        // Try the requested configuration first, then fall back to the most common alternatives.
        var configurationsToTry = new List<string>();
        void addIfMissing(string cfg)
        {
            if (!configurationsToTry.Contains(cfg, StringComparer.OrdinalIgnoreCase))
            {
                configurationsToTry.Add(cfg);
            }
        }

        addIfMissing(configuration);
        addIfMissing("Release");
        addIfMissing("Debug");

        string? finalAsmPath = null;
        string? chosenConfiguration = null;
        string assemblyName;
        string targetFramework;

        var projDir = Path.GetDirectoryName(Path.GetFullPath(csprojPath)) ?? "";

        foreach (var cfg in configurationsToTry)
        {
            project.SetProperty("Configuration", cfg);
            project.ReevaluateIfNecessary();

            assemblyName = project.GetPropertyValue("AssemblyName");
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = Path.GetFileNameWithoutExtension(csprojPath);
            }

            var outputPath = project.GetPropertyValue("OutputPath")
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine("bin", cfg);
            }

            targetFramework = project.GetPropertyValue("TargetFramework");

            var fullOutputPath = Path.GetFullPath(Path.Combine(projDir, outputPath));
            foreach (var ext in new[] { ".dll", ".exe" })
            {
                var candidate = Path.Combine(fullOutputPath, assemblyName + ext);
                if (File.Exists(candidate))
                {
                    finalAsmPath = candidate;
                    chosenConfiguration = cfg;
                    break;
                }
            }

            if (finalAsmPath != null)
            {
                break;
            }
        }

        // If not found, fall back to a reasonable default path for the originally requested configuration
        assemblyName = project.GetPropertyValue("AssemblyName");
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = Path.GetFileNameWithoutExtension(csprojPath);
        }
        targetFramework = project.GetPropertyValue("TargetFramework");
        if (finalAsmPath == null)
        {
            var fallbackOutput = project.GetPropertyValue("OutputPath");
            if (string.IsNullOrWhiteSpace(fallbackOutput))
            {
                fallbackOutput = Path.Combine("bin", configuration);
            }
            var fullFallback = Path.GetFullPath(Path.Combine(projDir, fallbackOutput));
            finalAsmPath = Path.Combine(fullFallback, assemblyName + ".dll");
            _logger.LogWarning("Assembly not found for configurations [{Configs}], assuming {Path}",
                string.Join(", ", configurationsToTry), finalAsmPath);
        }

        // Search for project.assets.json
        // Usually: obj/{TargetFramework}/project.assets.json or obj/project.assets.json
        var assets1 = Path.Combine(projDir, "obj", targetFramework ?? "", "project.assets.json");
        var assets2 = Path.Combine(projDir, "obj", "project.assets.json");
        var assetsFile = File.Exists(assets1) ? assets1
                         : File.Exists(assets2) ? assets2
                         : "";

        if (string.IsNullOrEmpty(assetsFile))
        {
            _logger.LogWarning("project.assets.json not found in {0}", projDir);
        }
        
        projectCollection.UnloadAllProjects();

        return (finalAsmPath, assetsFile, targetFramework ?? "netX");
    }
}