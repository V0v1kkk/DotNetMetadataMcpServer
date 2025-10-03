using System.Text.Json.Serialization;
using DotNetMetadataMcpServer.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace DotNetMetadataMcpServer.Models
{
    public class TypeToolResponse : PagedResponse
    {
        public IEnumerable<SimpleTypeInfo> TypeData { get; set; } = [];
    }

    public class SimpleTypeInfo
    {
        [Required]
        public required string FullName { get; init; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Implements { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Constructors { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Methods { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Properties { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Fields { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Events { get; set; }
    }
}