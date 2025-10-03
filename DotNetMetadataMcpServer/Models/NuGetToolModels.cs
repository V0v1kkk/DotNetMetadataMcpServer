using DotNetMetadataMcpServer.Models.Base;

namespace DotNetMetadataMcpServer.Models
{
    public class NuGetPackageInfo
    {
        public required string Id { get; set; }
        public required string Version { get; set; }
        public string? Description { get; set; }
        public string? Authors { get; set; }
        public long DownloadCount { get; set; }
        public DateTimeOffset? Published { get; set; }
        public List<NuGetPackageDependencyGroup> DependencyGroups { get; set; } = [];
    }

    public class NuGetPackageDependencyGroup
    {
        public required string TargetFramework { get; set; }
        public List<NuGetPackageDependency> Dependencies { get; set; } = [];
    }

    public class NuGetPackageDependency
    {
        public required string Id { get; set; }
        public required string VersionRange { get; set; }
    }

    public class NuGetPackageSearchResponse : PagedResponse
    {
        public List<NuGetPackageInfo> Packages { get; set; } = [];
    }

    public class NuGetPackageVersionsResponse : PagedResponse
    {
        public required string PackageId { get; set; }
        public List<NuGetPackageInfo> Versions { get; set; } = [];
    }
}