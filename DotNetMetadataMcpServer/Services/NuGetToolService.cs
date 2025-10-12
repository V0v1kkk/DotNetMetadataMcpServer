using DotNetMetadataMcpServer.Configuration;
using DotNetMetadataMcpServer.Helpers;
using DotNetMetadataMcpServer.Models;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace DotNetMetadataMcpServer.Services
{
    public class NuGetToolService
    {
        private readonly ILogger<NuGetToolService> _logger;
        private readonly List<SourceRepository> _repositories;
        private readonly NuGet.Common.ILogger _nugetLogger;
        private readonly CancellationToken _cancellationToken;

        public NuGetToolService(ILogger<NuGetToolService> logger, IOptions<ToolsConfiguration> configuration)
        {
            _logger = logger;
            _nugetLogger = NullLogger.Instance;
            _cancellationToken = CancellationToken.None;
            
            // Initialize repositories from configuration
            // Priority is determined by order in configuration (first = highest priority)
            _repositories = new List<SourceRepository>();
            var sources = configuration.Value.NuGetSources.Where(s => s.Enabled).ToList();
            
            // Add default nuget.org ONLY if no sources configured
            if (!sources.Any())
            {
                _logger.LogWarning("No NuGet sources configured, adding default nuget.org");
                sources.Add(new NuGetSourceConfiguration 
                { 
                    Name = "nuget.org", 
                    Url = "https://api.nuget.org/v3/index.json",
                    Enabled = true
                });
            }
            
            // Repositories are added in order - this order determines priority
            foreach (var source in sources)
            {
                try
                {
                    var repository = Repository.Factory.GetCoreV3(source.Url);
                    _repositories.Add(repository);
                    _logger.LogInformation("NuGet source added with priority {Priority}: {Name} ({Url})", 
                        _repositories.Count, source.Name, source.Url);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add NuGet source: {Name} ({Url})", source.Name, source.Url);
                }
            }
            
            if (!_repositories.Any())
            {
                throw new InvalidOperationException("No valid NuGet sources configured");
            }
        }

        public async Task<NuGetPackageSearchResponse> SearchPackagesAsync(
            string searchQuery, 
            List<string> filters, 
            bool includePrerelease, 
            int pageNumber, 
            int pageSize)
        {
            _logger.LogInformation("Searching NuGet packages with query: {Query}, includePrerelease: {IncludePrerelease} across {SourceCount} sources", 
                searchQuery, includePrerelease, _repositories.Count);
            
            try
            {
                // Search across all configured repositories in parallel for performance
                var searchTasks = _repositories.Select((repo, index) => new { Repo = repo, Priority = index })
                    .Select(async item =>
                    {
                        try
                        {
                            var searchResource = await item.Repo.GetResourceAsync<PackageSearchResource>();
                            var results = await searchResource.SearchAsync(
                                searchQuery,
                                new SearchFilter(includePrerelease),
                                skip: 0,
                                take: 100,
                                _nugetLogger,
                                _cancellationToken);
                            return new { Results = results, Priority = item.Priority };
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to search in repository at priority {Priority}", item.Priority);
                            return new { Results = Enumerable.Empty<IPackageSearchMetadata>(), Priority = item.Priority };
                        }
                    });

                var allResults = await Task.WhenAll(searchTasks);
                var packages = new Dictionary<string, NuGetPackageInfo>();
                
                // Process results in priority order (lower priority number = higher priority)
                foreach (var resultSet in allResults.OrderBy(r => r.Priority))
                {
                    foreach (var package in resultSet.Results)
                    {
                        // Priority-based deduplication: packages from higher priority sources (earlier in config)
                        // take precedence over packages from lower priority sources
                        if (!packages.ContainsKey(package.Identity.Id))
                        {
                            packages[package.Identity.Id] = new NuGetPackageInfo
                            {
                                Id = package.Identity.Id,
                                Version = package.Identity.Version.ToString(),
                                Description = package.Description,
                                Authors = package.Authors,
                                DownloadCount = package.DownloadCount ?? 0,
                                Published = package.Published
                            };
                        }
                    }
                }

                var packageList = packages.Values.ToList();

                // Apply additional filtering if needed
                if (filters.Any())
                {
                    var predicates = filters.Select(FilteringHelper.PrepareFilteringPredicate).ToList();
                    packageList = packageList
                        .Where(p => predicates.Any(predicate => 
                            predicate.Invoke(p.Id) || 
                            (p.Description != null && predicate.Invoke(p.Description))))
                        .ToList();
                }
                
                // Apply pagination
                var (paged, availablePages) = PaginationHelper.FilterAndPaginate(
                    packageList, 
                    _ => true, 
                    pageNumber, 
                    pageSize);
                
                return new NuGetPackageSearchResponse
                {
                    Packages = paged,
                    CurrentPage = pageNumber,
                    AvailablePages = availablePages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching NuGet packages with query: {Query}", searchQuery);
                throw;
            }
        }

        public async Task<NuGetPackageVersionsResponse> GetPackageVersionsAsync(
            string packageId, 
            List<string> filters, 
            bool includePrerelease, 
            int pageNumber, 
            int pageSize)
        {
            _logger.LogInformation("Getting versions for NuGet package: {PackageId}, includePrerelease: {IncludePrerelease} across {SourceCount} sources", 
                packageId, includePrerelease, _repositories.Count);
            
            try
            {
                // Query all repositories in parallel for performance
                var metadataTasks = _repositories.Select((repo, index) => new { Repo = repo, Priority = index })
                    .Select(async item =>
                    {
                        try
                        {
                            var metadataResource = await item.Repo.GetResourceAsync<PackageMetadataResource>();
                            var results = await metadataResource.GetMetadataAsync(
                                packageId,
                                includePrerelease,
                                includeUnlisted: false,
                                new SourceCacheContext(),
                                _nugetLogger,
                                _cancellationToken);
                            return new { Results = results, Priority = item.Priority };
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get metadata from repository at priority {Priority}", item.Priority);
                            return new { Results = Enumerable.Empty<IPackageSearchMetadata>(), Priority = item.Priority };
                        }
                    });

                var allMetadata = await Task.WhenAll(metadataTasks);
                var versionDict = new Dictionary<string, NuGetPackageInfo>();
                
                // Process results in priority order (lower priority number = higher priority)
                foreach (var metadataSet in allMetadata.OrderBy(m => m.Priority))
                {
                    foreach (var metadata in metadataSet.Results)
                    {
                        var version = metadata.Identity.Version.ToString();
                        
                        // Priority-based deduplication: versions from higher priority sources take precedence
                        if (!versionDict.ContainsKey(version))
                        {
                            var packageInfo = new NuGetPackageInfo
                            {
                                Id = metadata.Identity.Id,
                                Version = version,
                                Description = metadata.Description,
                                Authors = metadata.Authors,
                                DownloadCount = metadata.DownloadCount ?? 0,
                                Published = metadata.Published,
                                DependencyGroups = []
                            };
                            
                            // Add dependency groups
                            foreach (var group in metadata.DependencySets)
                            {
                                var dependencyGroup = new NuGetPackageDependencyGroup
                                {
                                    TargetFramework = group.TargetFramework.ToString(),
                                    Dependencies = []
                                };
                                
                                foreach (var dependency in group.Packages)
                                {
                                    dependencyGroup.Dependencies.Add(new NuGetPackageDependency
                                    {
                                        Id = dependency.Id,
                                        VersionRange = dependency.VersionRange.ToString()
                                    });
                                }
                                
                                packageInfo.DependencyGroups.Add(dependencyGroup);
                            }
                            
                            versionDict[version] = packageInfo;
                        }
                    }
                }

                var versions = versionDict.Values.ToList();

                // Apply additional filtering if needed
                if (filters.Any())
                {
                    var predicates = filters.Select(FilteringHelper.PrepareFilteringPredicate).ToList();
                    versions = versions
                        .Where(v => predicates.Any(predicate => 
                            predicate.Invoke(v.Version) || 
                            (v.Description != null && predicate.Invoke(v.Description))))
                        .ToList();
                }
                
                // Apply pagination
                var (paged, availablePages) = PaginationHelper.FilterAndPaginate(
                    versions, 
                    _ => true, 
                    pageNumber, 
                    pageSize);
                
                return new NuGetPackageVersionsResponse
                {
                    PackageId = packageId,
                    Versions = paged,
                    CurrentPage = pageNumber,
                    AvailablePages = availablePages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for NuGet package: {PackageId}", packageId);
                throw;
            }
        }
    }
}