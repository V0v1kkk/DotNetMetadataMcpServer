# .NET Types Explorer MCP Server

A Model Context Protocol (MCP) server that provides detailed type information from .NET projects for AI coding agents.

## Overview

The .NET Types Explorer MCP Server is a powerful tool designed to help AI coding agents understand and work with .NET codebases. 
It provides a structured way to explore assemblies, namespaces, and types in .NET projects, making it easier for AI agents to generate accurate and context-aware code suggestions.

The server uses reflection to extract detailed type information from compiled .NET assemblies, including classes, interfaces, methods, properties, fields, and events. 
This information is then made available through a set of tools that can be used by AI agents to explore the codebase in a systematic way.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. 

## Features

- **Assembly Exploration**: Retrieve a list of all assemblies referenced by a .NET project
- **Namespace Exploration**: Discover all namespaces within specified assemblies
- **Type Exploration**: Get detailed information about types within specified namespaces, including:
  - Full type names with generic parameters
  - Implemented interfaces
  - Constructors with parameters
  - Methods with return types and parameters
  - Properties with types and accessors
  - Fields with types and modifiers
  - Events with handler types
- **NuGet Package Search**: Search for NuGet packages on nuget.org with filtering and pagination
- **NuGet Package Version Information**: Retrieve version history and dependency information for specific NuGet packages
- **Filtering**: Apply wildcard filters to narrow down results
- **Pagination**: Handle large result sets with built-in pagination

## Roadmap

- [x] Add NuGet integration to provide information about actual package versions
- [x] Migrate to official Model Context Protocol C# SDK ([modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk))
- [x] Support for multiple NuGet package sources (custom/private feeds)
- [ ] Add dependency graph building capabilities
- [ ] Improve multi-project scenario

## Prerequisites

- .NET 9.0 SDK or later
- A .NET project that you want to explore

## Configuration

### NuGet Package Sources

The server supports querying multiple NuGet package sources simultaneously. Configure custom sources in `appsettings.json`:

```json
{
  "Tools": {
    "DefaultPageSize": 20,
    "IntendResponse": false,
    "NuGetSources": [
      {
        "Name": "nuget.org",
        "Url": "https://api.nuget.org/v3/index.json",
        "Enabled": true
      },
      {
        "Name": "MyPrivateFeed",
        "Url": "https://my-company.com/nuget/v3/index.json",
        "Enabled": true
      },
      {
        "Name": "dotnet-core (MyGet)",
        "Url": "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
        "Enabled": true,
        "Comment": "Example: MyGet public feed for .NET Core preview packages"
      }
    ]
  }
}
```

**Configuration Options:**
- **Name**: Friendly name for the package source
- **Url**: Full URL to the NuGet v3 API endpoint (must end with `/v3/index.json`)
- **Enabled**: Set to `false` to temporarily disable a source without removing it

**Behavior:**
- All enabled sources are queried in parallel for better performance
- Results are automatically deduplicated by package ID and version
- If one source fails, the server continues with the remaining sources
- **Default source behavior**: nuget.org is added ONLY if no sources are configured in `appsettings.json`

**Source Priority and Deduplication:**

When the same package is found in multiple sources, the server uses a **priority-based strategy**:

1. **Priority is determined by order**: Sources listed first in configuration have **higher priority**
2. **Deterministic results**: The first source in the list always takes precedence for duplicate packages
3. **Parallel queries**: Despite parallel execution, results are processed in priority order
4. **Metadata consistency**: Package metadata (description, download count, etc.) always comes from the highest priority source containing that package

**Example:**
```json
{
  "NuGetSources": [
    { "Name": "Internal", "Url": "https://internal.company.com/nuget/v3/index.json", "Enabled": true },
    { "Name": "nuget.org", "Url": "https://api.nuget.org/v3/index.json", "Enabled": true }
  ]
}
```
In this configuration, if a package exists in both sources, information from "Internal" will be used.

## Installation

### Option 1: Native Installation

1. Clone the repository
2. Build the project:
   ```bash
   dotnet build -c Release
   ```
3. Publish the project:
   ```bash
   dotnet publish -c Release -r <runtime-identifier> --self-contained false
   ```
   Replace `<runtime-identifier>` with your target platform (e.g., `win-x64`, `linux-x64`, `osx-x64`).

### Option 2: Docker

The server is available as a Docker container, providing better isolation and easier deployment:

1. **Pull the image** from Docker Hub:
   ```bash
   docker pull vrogozhin/dotnet-types-explorer-mcp:latest
   ```

2. **Or build locally**:
   ```bash
   git clone https://github.com/V0v1kkk/DotNetMetadataMcpServer.git
   cd DotNetMetadataMcpServer
   docker build -t dotnet-types-explorer-mcp .
   ```

3. **Run the container**:
   ```bash
   docker run --rm -i \
     -v /path/to/your/dotnet/projects:/workspace \
     vrogozhin/dotnet-types-explorer-mcp:latest
   ```

**Benefits of Docker deployment:**
- No need to install .NET SDK on your machine
- Isolated environment prevents conflicts
- Consistent behavior across different operating systems
- Includes MSBuild and all required dependencies
- Easy updates by pulling the latest image

## Configuration

### Native Configuration

To use the .NET Types Explorer MCP Server with an AI agent, you need to configure it in your MCP settings file. Here's an example configuration:

```json
{
  "mcpServers": {
    "dotnet-types-explorer": {
      "command": "/path/to/DotNetMetadataMcpServer",
      "args": [ "--homeEnvVariable", "/home/user" ],
      "disabled": false,
      "alwaysAllow": [],
      "timeout": 300
    }
  }
}
```

Replace `/path/to/DotNetMetadataMcpServer` with the actual path to the published executable, and `/home/user` with your home directory.

### Docker Configuration

For Docker deployment, configure your MCP client as follows:

```json
{
  "mcpServers": {
    "dotnet-types-explorer": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "-v",
        "/path/to/your/dotnet/projects:/workspace",
        "vrogozhin/dotnet-types-explorer-mcp:latest"
      ],
      "disabled": false,
      "alwaysAllow": [],
      "timeout": 300
    }
  }
}
```

Replace `/path/to/your/dotnet/projects` with the directory containing your .NET projects. The container will have access to all projects in this directory.

## Important Limitations

- **The project must be built before scanning.** The server relies on compiled assemblies to extract type information, so make sure to build your project before using the tools.
- **The tool doesn't follow references to other projects.** It only inspects the specified project and its NuGet dependencies. If you need to analyze multiple projects, you'll need to scan each one separately.

### How project outputs are located

When scanning a project, the server needs the compiled assembly path. `MsBuildHelper` evaluates the project and attempts to locate the output assembly by probing configurations in this order:

- the requested configuration (by default Debug), then
- Release, and if still not found,
- falls back to checking Debug again as a safe default.

The first existing output wins. If no output is found, a reasonable default path is assumed and a warning is logged. This makes local runs (often Debug) and CI runs (often Release) behave consistently. Ensure you build the project in one of these configurations before scanning.

## Usage

The server provides five main tools that can be used by AI agents:

1. **ReferencedAssembliesExplorer**: Retrieves referenced assemblies from a .NET project
2. **NamespacesExplorer**: Retrieves namespaces from specified assemblies
3. **NamespaceTypes**: Retrieves types from specified namespaces
4. **NuGetPackageSearch**: Searches for NuGet packages on nuget.org with filtering and pagination
5. **NuGetPackageVersions**: Retrieves version history and dependency information for specific NuGet packages

This tool has been tested with the [Roo Code Visual Studio extension](https://marketplace.visualstudio.com/items?itemName=RooVeterinaryInc.roo-cline), an AI coding assistant that supports the Model Context Protocol. You can find more information about Roo Code on [GitHub](https://github.com/RooVetGit/Roo-Code?tab=readme-ov-file).

> It's possible to use `.clinerules` file to instruct your codding assistant to use the MCP server.


## AI Code Assistant Instructions

If you work with .NET projects that have NuGet package references and need to write code using those packages, you should explore the API of those packages systematically using the Dotnet Type Explorer MCP server. This is especially important when you're not familiar with the package's API or when documentation is limited.

When working with a .NET project:

1. First, make sure the project is built. This tool relies on compiled assemblies to extract type information.

2. Start by retrieving all assemblies referenced by the project file using the ReferencedAssembliesExplorer tool. This gives you a list of all available assemblies.

3. Focus on third-party libraries and NuGet packages, not the Base Class Library (BCL) types like System.* or Microsoft.* which are well-documented elsewhere.

4. Once you've identified the relevant assemblies, use the NamespacesExplorer tool to discover the namespaces within those assemblies. This helps you understand how the library is organized.

5. After identifying the relevant namespaces, use the NamespaceTypes tool to retrieve detailed information about the types within those namespaces in which you are interested in. This includes methods, properties, fields, events, and more.

6. Only use filtering when you're overwhelmed by the amount of data or when you know exactly what you're looking for.

7. If you need to find specific NuGet packages or explore their versions and dependencies, use the NuGetPackageSearch tool to search for packages by name or keywords.

8. When you need detailed information about a specific NuGet package, including its version history and dependencies, use the NuGetPackageVersions tool. This is particularly useful when you need to understand compatibility requirements or dependency chains.

9. Use the filtering capabilities of the NuGet tools to narrow down results when searching for specific versions or packages with particular naming patterns.

Remember that this tool only inspects the specified project and its NuGet dependencies. It doesn't follow references to other projects in the solution. If you need to analyze multiple projects, you'll need to scan each one separately.

This top-down approach (assemblies → namespaces → types) is the most efficient way to explore and understand .NET libraries when you need to write code that uses them. It's particularly valuable for third-party libraries where the API might not be immediately obvious or well-documented. The NuGet tools complement this approach by providing direct access to package information without requiring the packages to be already referenced in the project.

## How It Works

The server uses the following process to extract type information:

1. **Project Evaluation**: Uses MSBuild to evaluate the project file and find the compiled assembly
2. **Assembly Loading**: Loads the compiled assembly and its dependencies
3. **Type Reflection**: Uses reflection to extract detailed information about types
4. **Filtering and Pagination**: Applies filters and pagination to the results
5. **Response Formatting**: Formats the results as JSON and returns them to the client

## API Reference

### ReferencedAssembliesExplorer

Retrieves referenced assemblies based on filters and pagination.

**Input Schema:**
```json
{
  "type": "object",
  "properties": {
    "ProjectFileAbsolutePath": {
      "type": "string"
    },
    "PageNumber": {
      "type": "integer"
    },
    "FullTextFiltersWithWildCardSupport": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    }
  },
  "required": [
    "ProjectFileAbsolutePath"
  ]
}
```

**Response:**
```json
{
  "AssemblyNames": ["Assembly1", "Assembly2", ...],
  "CurrentPage": 1,
  "AvailablePages": [1, 2, ...]
}
```

### NamespacesExplorer

Retrieves namespaces from specified assemblies supporting filters and pagination.

**Input Schema:**
```json
{
  "type": "object",
  "properties": {
    "ProjectFileAbsolutePath": {
      "type": "string"
    },
    "AssemblyNames": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    },
    "PageNumber": {
      "type": "integer"
    },
    "FullTextFiltersWithWildCardSupport": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    }
  },
  "required": [
    "ProjectFileAbsolutePath"
  ]
}
```

**Response:**
```json
{
  "Namespaces": ["Namespace1", "Namespace2", ...],
  "CurrentPage": 1,
  "AvailablePages": [1, 2, ...]
}
```

### NamespaceTypes

Retrieves types from specified namespaces supporting filters and pagination.

**Input Schema:**
```json
{
  "type": "object",
  "properties": {
    "ProjectFileAbsolutePath": {
      "type": "string"
    },
    "Namespaces": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    },
    "PageNumber": {
      "type": "integer"
    },
    "FullTextFiltersWithWildCardSupport": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    }
  },
  "required": [
    "ProjectFileAbsolutePath"
  ]
}
```

**Response:**
```json
{
  "TypeData": [
    {
      "FullName": "Namespace.TypeName",
      "Implements": ["Interface1", "Interface2", ...],
      "Constructors": ["(param1, param2)", ...],
      "Methods": ["ReturnType MethodName(param1, param2)", ...],
      "Properties": ["PropertyType PropertyName { get; set; }", ...],
      "Fields": ["FieldType FieldName", ...],
      "Events": ["event EventHandlerType EventName", ...]
    },
    ...
  ],
  "CurrentPage": 1,
  "AvailablePages": [1, 2, ...]
}
```

### NuGetPackageSearch

Searches for NuGet packages on nuget.org with support for filtering and pagination.

**Input Schema:**
```json
{
  "type": "object",
  "properties": {
    "SearchQuery": {
      "type": "string"
    },
    "IncludePrerelease": {
      "type": "boolean"
    },
    "PageNumber": {
      "type": "integer"
    },
    "FullTextFiltersWithWildCardSupport": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    }
  },
  "required": [
    "SearchQuery"
  ]
}
```

**Response:**
```json
{
  "Packages": [
    {
      "Id": "Newtonsoft.Json",
      "Version": "13.0.3",
      "Description": "Json.NET is a popular high-performance JSON framework for .NET",
      "Authors": "James Newton-King",
      "DownloadCount": 1000000,
      "Published": "2023-03-08T00:00:00Z"
    },
    ...
  ],
  "CurrentPage": 1,
  "AvailablePages": [1, 2, ...]
}
```

### NuGetPackageVersions

Retrieves version history and dependency information for a specific NuGet package.

**Input Schema:**
```json
{
  "type": "object",
  "properties": {
    "PackageId": {
      "type": "string"
    },
    "IncludePrerelease": {
      "type": "boolean"
    },
    "PageNumber": {
      "type": "integer"
    },
    "FullTextFiltersWithWildCardSupport": {
      "type": "array",
      "items": {
        "type": [
          "string",
          "null"
        ]
      }
    }
  },
  "required": [
    "PackageId"
  ]
}
```

**Response:**
```json
{
  "PackageId": "Newtonsoft.Json",
  "Versions": [
    {
      "Id": "Newtonsoft.Json",
      "Version": "13.0.3",
      "Description": "Json.NET is a popular high-performance JSON framework for .NET",
      "Authors": "James Newton-King",
      "DownloadCount": 1000000,
      "Published": "2023-03-08T00:00:00Z",
      "DependencyGroups": [
        {
          "TargetFramework": ".NETStandard2.0",
          "Dependencies": [
            {
              "Id": "System.Text.Json",
              "VersionRange": "6.0.0"
            }
          ]
        }
      ]
    },
    ...
  ],
  "CurrentPage": 1,
  "AvailablePages": [1, 2, ...]
}
```

## Example Usage Scenario

Here's an example of how an AI agent might use the .NET Types Explorer MCP Server to explore a UI framework API:

1. **Retrieve Assemblies with Filtering**:
   ```json
   {
     "ProjectFileAbsolutePath": "/home/user/Projects/MyApp/MyApp.csproj",
     "PageNumber": 1,
     "FullTextFiltersWithWildCardSupport": ["Avalonia*"]
   }
   ```

   Response:
   ```json
   {
     "AssemblyNames": [
       "Avalonia",
       "Avalonia.Controls.ColorPicker",
       "Avalonia.Controls.DataGrid",
       "Avalonia.Desktop",
       "Avalonia.Diagnostics",
       "Avalonia.Fonts.Inter",
       "Avalonia.FreeDesktop",
       "Avalonia.Native",
       "Avalonia.ReactiveUI",
       "Avalonia.Remote.Protocol"
     ],
     "CurrentPage": 1,
     "AvailablePages": [1, 2]
   }
   ```

2. **Retrieve Namespaces with Targeted Filtering**:
   ```json
   {
     "ProjectFileAbsolutePath": "/home/user/Projects/MyApp/MyApp.csproj",
     "AssemblyNames": ["Avalonia"],
     "PageNumber": 1,
     "FullTextFiltersWithWildCardSupport": ["*Media*", "*Image*"]
   }
   ```

   Response:
   ```json
   {
     "Namespaces": [
       "Avalonia.Media",
       "Avalonia.Media.Transformation",
       "Avalonia.Media.TextFormatting",
       "Avalonia.Media.TextFormatting.Unicode",
       "Avalonia.Media.Immutable",
       "Avalonia.Media.Imaging",
       "Avalonia.Media.Fonts"
     ],
     "CurrentPage": 1,
     "AvailablePages": [1]
   }
   ```

3. **Retrieve Types from a Specific Namespace**:
   ```json
   {
     "ProjectFileAbsolutePath": "/home/user/Projects/MyApp/MyApp.csproj",
     "Namespaces": ["Avalonia.Media.Imaging"],
     "PageNumber": 1,
     "FullTextFiltersWithWildCardSupport": []
   }
   ```

   Response (partial):
   ```json
   {
     "TypeData": [
       {
         "FullName": "Avalonia.Media.Imaging.Bitmap",
         "Implements": [
           "IBitmap",
           "IImage",
           "IDisposable",
           "IImageBrushSource"
         ],
         "Constructors": [
           "(String fileName)",
           "(Stream stream)",
           "(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, Int32 stride)"
         ],
         "Methods": [
           "static Avalonia.Media.Imaging.Bitmap DecodeToWidth(System.IO.Stream stream, System.Int32 width, Avalonia.Media.Imaging.BitmapInterpolationMode? interpolationMode = null)",
           "static Avalonia.Media.Imaging.Bitmap DecodeToHeight(System.IO.Stream stream, System.Int32 height, Avalonia.Media.Imaging.BitmapInterpolationMode? interpolationMode = null)",
           "Avalonia.Media.Imaging.Bitmap CreateScaledBitmap(Avalonia.PixelSize destinationSize, Avalonia.Media.Imaging.BitmapInterpolationMode? interpolationMode = null)",
           "virtual System.Void Dispose()"
         ],
         "Properties": [
           "virtual Avalonia.Vector Dpi { get; }",
           "virtual Avalonia.Size Size { get; }",
           "virtual Avalonia.PixelSize PixelSize { get; }"
         ]
       }
     ],
     "CurrentPage": 1,
     "AvailablePages": [1, 2]
   }
   ```

4. **Apply Specific Filters to Find Related Types**:
   ```json
   {
     "ProjectFileAbsolutePath": "/home/user/Projects/MyApp/MyApp.csproj",
     "Namespaces": ["Avalonia.Platform"],
     "PageNumber": 1,
     "FullTextFiltersWithWildCardSupport": ["*Framebuffer*", "*Pixel*"]
   }
   ```

   Response:
   ```json
   {
     "TypeData": [
       {
         "FullName": "Avalonia.Platform.ILockedFramebuffer",
         "Implements": ["IDisposable"],
         "Methods": [
           "abstract System.IntPtr get_Address()",
           "abstract Avalonia.PixelSize get_Size()",
           "abstract System.Int32 get_RowBytes()",
           "abstract Avalonia.Vector get_Dpi()",
           "abstract Avalonia.Platform.PixelFormat get_Format()"
         ],
         "Properties": [
           "abstract System.IntPtr Address { get; }",
           "abstract Avalonia.PixelSize Size { get; }",
           "abstract System.Int32 RowBytes { get; }",
           "abstract Avalonia.Vector Dpi { get; }",
           "abstract Avalonia.Platform.PixelFormat Format { get; }"
         ]
       }
     ],
     "CurrentPage": 1,
     "AvailablePages": [1]
   }
   ```
  
## NuGet Package Examples

Here are examples of how an AI agent might use the NuGet tools to explore package information:

1. **Search for NuGet Packages**:
  ```json
  {
    "SearchQuery": "EntityFrameworkCore",
    "IncludePrerelease": false,
    "PageNumber": 1,
    "FullTextFiltersWithWildCardSupport": []
  }
  ```

  Response:
  ```json
  {
    "Packages": [
      {
        "Id": "Microsoft.EntityFrameworkCore",
        "Version": "7.0.0",
        "Description": "Entity Framework Core is a lightweight and extensible version of the popular Entity Framework data access technology.",
        "Authors": "Microsoft",
        "DownloadCount": 42000000,
        "Published": "2022-11-08T00:00:00Z"
      },
      {
        "Id": "Microsoft.EntityFrameworkCore.SqlServer",
        "Version": "7.0.0",
        "Description": "Microsoft SQL Server database provider for Entity Framework Core.",
        "Authors": "Microsoft",
        "DownloadCount": 38000000,
        "Published": "2022-11-08T00:00:00Z"
      }
    ],
    "CurrentPage": 1,
    "AvailablePages": [1, 2, 3, 4, 5]
  }
  ```

2. **Search with Filtering**:
  ```json
  {
    "SearchQuery": "Json",
    "IncludePrerelease": false,
    "PageNumber": 1,
    "FullTextFiltersWithWildCardSupport": ["Newtonsoft*"]
  }
  ```

  Response:
  ```json
  {
    "Packages": [
      {
        "Id": "Newtonsoft.Json",
        "Version": "13.0.3",
        "Description": "Json.NET is a popular high-performance JSON framework for .NET",
        "Authors": "James Newton-King",
        "DownloadCount": 1250000000,
        "Published": "2023-03-08T00:00:00Z"
      },
      {
        "Id": "Newtonsoft.Json.Bson",
        "Version": "1.0.2",
        "Description": "Json.NET BSON adds support for reading and writing BSON",
        "Authors": "James Newton-King",
        "DownloadCount": 120000000,
        "Published": "2020-01-01T00:00:00Z"
      }
    ],
    "CurrentPage": 1,
    "AvailablePages": [1]
  }
  ```

3. **Get Package Version Information**:
  ```json
  {
    "PackageId": "Newtonsoft.Json",
    "IncludePrerelease": false,
    "PageNumber": 1,
    "FullTextFiltersWithWildCardSupport": []
  }
  ```

  Response:
  ```json
  {
    "PackageId": "Newtonsoft.Json",
    "Versions": [
      {
        "Id": "Newtonsoft.Json",
        "Version": "13.0.3",
        "Description": "Json.NET is a popular high-performance JSON framework for .NET",
        "Authors": "James Newton-King",
        "DownloadCount": 1250000000,
        "Published": "2023-03-08T00:00:00Z",
        "DependencyGroups": [
          {
            "TargetFramework": ".NETStandard2.0",
            "Dependencies": []
          },
          {
            "TargetFramework": ".NETFramework4.5",
            "Dependencies": []
          }
        ]
      },
      {
        "Id": "Newtonsoft.Json",
        "Version": "13.0.2",
        "Description": "Json.NET is a popular high-performance JSON framework for .NET",
        "Authors": "James Newton-King",
        "DownloadCount": 980000000,
        "Published": "2022-11-24T00:00:00Z",
        "DependencyGroups": [
          {
            "TargetFramework": ".NETStandard2.0",
            "Dependencies": []
          },
          {
            "TargetFramework": ".NETFramework4.5",
            "Dependencies": []
          }
        ]
      }
    ],
    "CurrentPage": 1,
    "AvailablePages": [1, 2, 3, 4, 5]
  }
  ```

4. **Get Specific Package Version with Filtering**:
  ```json
  {
    "PackageId": "Microsoft.EntityFrameworkCore",
    "IncludePrerelease": false,
    "PageNumber": 1,
    "FullTextFiltersWithWildCardSupport": ["7.0.0"]
  }
  ```

  Response:
  ```json
  {
    "PackageId": "Microsoft.EntityFrameworkCore",
    "Versions": [
      {
        "Id": "Microsoft.EntityFrameworkCore",
        "Version": "7.0.0",
        "Description": "Entity Framework Core is a lightweight and extensible version of the popular Entity Framework data access technology.",
        "Authors": "Microsoft",
        "DownloadCount": 42000000,
        "Published": "2022-11-08T00:00:00Z",
        "DependencyGroups": [
          {
            "TargetFramework": "net6.0",
            "Dependencies": [
              {
                "Id": "Microsoft.Extensions.Caching.Memory",
                "VersionRange": "7.0.0"
              },
              {
                "Id": "Microsoft.Extensions.DependencyInjection",
                "VersionRange": "7.0.0"
              },
              {
                "Id": "Microsoft.Extensions.Logging",
                "VersionRange": "7.0.0"
              }
            ]
          }
        ]
      }
    ],
    "CurrentPage": 1,
    "AvailablePages": [1]
  }
  ```

## License

This project is licensed under the Apache 2.0 License - see the LICENSE file for details.
