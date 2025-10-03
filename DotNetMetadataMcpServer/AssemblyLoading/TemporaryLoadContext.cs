using System.Reflection;
using System.Runtime.Loader;

namespace DotNetMetadataMcpServer;

internal sealed class TemporaryLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly string _baseDirectory;
    private readonly ILogger _logger;

    public TemporaryLoadContext(string baseDirectory, ILogger logger)
        : base("DotNetMetadataMcpServer.Reflection.LoadContext", isCollectible: true)
    {
        _baseDirectory = baseDirectory;
        _logger = logger;
        Resolving += OnResolving;
    }

    public Assembly? LoadFromFileWithoutLock(string assemblyPath)
    {
        try
        {
            var peBytes = File.ReadAllBytes(assemblyPath);
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (File.Exists(pdbPath))
            {
                var pdbBytes = File.ReadAllBytes(pdbPath);
                using var pe = new MemoryStream(peBytes, writable: false);
                using var pdb = new MemoryStream(pdbBytes, writable: false);
                return LoadFromStream(pe, pdb);
            }
            else
            {
                using var pe = new MemoryStream(peBytes, writable: false);
                return LoadFromStream(pe);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load from file without lock: {Path}", assemblyPath);
            return null;
        }
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        try
        {
            var candidatePath = Path.Combine(_baseDirectory, name.Name + ".dll");
            if (File.Exists(candidatePath))
            {
                var peBytes = File.ReadAllBytes(candidatePath);
                var pdbPath = Path.ChangeExtension(candidatePath, ".pdb");
                using var pe = new MemoryStream(peBytes, writable: false);
                if (File.Exists(pdbPath))
                {
                    var pdbBytes = File.ReadAllBytes(pdbPath);
                    using var pdb = new MemoryStream(pdbBytes, writable: false);
                    return LoadFromStream(pe, pdb);
                }
                return LoadFromStream(pe);
            }
            else
            {
                _logger.LogDebug("Dependency not found in base directory: {Dependency}", name.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve dependency {Name}", name);
        }
        return null;
    }

    public void UnloadAndCollect()
    {
        try
        {
            Resolving -= OnResolving;
            Unload();
            // Force GC to finalize/unload collectible ALC promptly (helps Windows release file handles)
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while unloading TemporaryLoadContext");
        }
    }

    public void Dispose()
    {
        UnloadAndCollect();
    }
}


