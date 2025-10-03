using System.ComponentModel;
using System.Text.Json;
using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace DotNetMetadataMcpServer.Tools;

[McpServerToolType]
public sealed class NamespaceTools
{
    [McpServerTool(Name = "NamespacesExplorer")]
    [Description("Retrieves namespaces from specified assemblies supporting filters and pagination (doesn't extract data from referenced projects. Notice that the project must be built before scanning.")]
    public static string GetNamespaces(
        NamespaceToolService namespaceToolService,
        IOptions<ToolsConfiguration> toolsConfiguration,
        ILogger<NamespaceTools> logger,
        [Description("The absolute path to the project file (.csproj)")] string projectFileAbsolutePath,
        [Description("The assembly names to filter by (without exe/dll extension). If empty, all assemblies are considered")] List<string>? assemblyNames = null,
        [Description("Full text filters with wildcard support (e.g., 'System.*', '*Json*')")] List<string>? fullTextFiltersWithWildCardSupport = null,
        [Description("Page number (1-based)")] int pageNumber = 1)
    {
        using var _ = logger.BeginScope("{NamespaceToolExecutionUid}", Guid.NewGuid());
        
        logger.LogInformation("Received request to retrieve namespaces for project: {ProjectPath}, Page: {PageNumber}", 
            projectFileAbsolutePath, pageNumber);
        
        try
        {
            var filters = fullTextFiltersWithWildCardSupport ?? new List<string>();
            var assemblies = assemblyNames ?? new List<string>();
            
            var result = namespaceToolService.GetNamespaces(
                projectFileAbsolutePath: projectFileAbsolutePath,
                allowedAssemblyNames: assemblies,
                filters: filters,
                pageNumber: pageNumber,
                pageSize: toolsConfiguration.Value.DefaultPageSize);
            
            logger.LogDebug("Namespaces retrieved successfully: {@NamespacesScanResult}", result);
            
            var json = toolsConfiguration.Value.IntendResponse
                ? JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                : JsonSerializer.Serialize(result);
            
            return json;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving namespaces");
            throw;
        }
    }
}

