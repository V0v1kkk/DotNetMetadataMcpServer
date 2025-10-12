namespace DotNetMetadataMcpServer.Configuration;

public class ToolsConfiguration
{
    public const string SectionName = "Tools";
    
    public int DefaultPageSize { get; set; } = 20;
    public bool IntendResponse { get; set; } = true;
    public List<NuGetSourceConfiguration> NuGetSources { get; set; } = new()
    {
        new NuGetSourceConfiguration
        {
            Name = "nuget.org",
            Url = "https://api.nuget.org/v3/index.json",
            Enabled = true
        }
    };
}

public class NuGetSourceConfiguration
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Comment { get; set; }
}