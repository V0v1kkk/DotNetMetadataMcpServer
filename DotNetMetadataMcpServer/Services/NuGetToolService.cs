using DotNetMetadataMcpServer.Helpers;
using DotNetMetadataMcpServer.Models;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace DotNetMetadataMcpServer.Services
{
    public class NuGetToolService
    {
        private readonly ILogger<NuGetToolService> _logger;
        private readonly SourceRepository _repository;
        private readonly NuGet.Common.ILogger _nugetLogger;
        private readonly CancellationToken _cancellationToken;

        public NuGetToolService(ILogger<NuGetToolService> logger)
        {
            _logger = logger;
            _nugetLogger = NullLogger.Instance;
            _cancellationToken = CancellationToken.None;
            
            // Connect to NuGet.org
            _repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        }

        public async Task<NuGetPackageSearchResponse> SearchPackagesAsync(
            string searchQuery, 
            List<string> filters, 
            bool includePrerelease, 
            int pageNumber, 
            int pageSize)
        {
            _logger.LogInformation("Searching NuGet packages with query: {Query}, includePrerelease: {IncludePrerelease}", 
                searchQuery, includePrerelease);
            
            try
            {
                // Prepare search resource
                var searchResource = await _repository.GetResourceAsync<PackageSearchResource>();

                // Search NuGet packages by query
                var searchResults = await searchResource.SearchAsync(
                    searchQuery,
                    new SearchFilter(includePrerelease),
                    skip: 0, // We'll handle pagination ourselves to apply additional filtering
                    take: 100, // Get more results than needed to allow for filtering
                    _nugetLogger,
                    _cancellationToken);

                var packages = new List<NuGetPackageInfo>();
                
                foreach (var package in searchResults)
                {
                    packages.Add(new NuGetPackageInfo
                    {
                        Id = package.Identity.Id,
                        Version = package.Identity.Version.ToString(),
                        Description = package.Description,
                        Authors = package.Authors,
                        DownloadCount = package.DownloadCount ?? 0,
                        Published = package.Published
                    });
                }

                // Apply additional filtering if needed
                if (filters.Any())
                {
                    var predicates = filters.Select(FilteringHelper.PrepareFilteringPredicate).ToList();
                    packages = packages
                        .Where(p => predicates.Any(predicate => 
                            predicate.Invoke(p.Id) || 
                            (p.Description != null && predicate.Invoke(p.Description))))
                        .ToList();
                }
                
                // Apply pagination
                var (paged, availablePages) = PaginationHelper.FilterAndPaginate(
                    packages, 
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
            _logger.LogInformation("Getting versions for NuGet package: {PackageId}, includePrerelease: {IncludePrerelease}", 
                packageId, includePrerelease);
            
            try
            {
                // Get detailed package metadata
                var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>();

                // Retrieve metadata for the package
                var metadataList = await metadataResource.GetMetadataAsync(
                    packageId,
                    includePrerelease,
                    includeUnlisted: false,
                    new SourceCacheContext(),
                    _nugetLogger,
                    _cancellationToken);

                var versions = new List<NuGetPackageInfo>();
                
                foreach (var metadata in metadataList)
                {
                    var packageInfo = new NuGetPackageInfo
                    {
                        Id = metadata.Identity.Id,
                        Version = metadata.Identity.Version.ToString(),
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
                    
                    versions.Add(packageInfo);
                }

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