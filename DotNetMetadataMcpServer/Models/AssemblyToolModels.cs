using DotNetMetadataMcpServer.Models.Base;

namespace DotNetMetadataMcpServer.Models
{
    public class AssemblyToolResponse : PagedResponse
    {
        public IEnumerable<string> AssemblyNames { get; set; } = new List<string>();
    }
}