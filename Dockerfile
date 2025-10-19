# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj", "DotNetMetadataMcpServer/"]
RUN dotnet restore "DotNetMetadataMcpServer/DotNetMetadataMcpServer.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/DotNetMetadataMcpServer"
RUN dotnet build "DotNetMetadataMcpServer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DotNetMetadataMcpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - using SDK image to include MSBuild (required for Microsoft.Build.Locator)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base
WORKDIR /app

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set HOME environment variable (required by the application)
ENV HOME=/root

# Create entrypoint script to handle HOME variable
RUN echo '#!/bin/bash\n\
if [ -z "$HOME" ]; then\n\
    export HOME=/root\n\
fi\n\
dotnet DotNetMetadataMcpServer.dll --homeEnvVariable "$HOME"' > /app/entrypoint.sh && \
    chmod +x /app/entrypoint.sh

# Use stdio transport (required for MCP)
ENTRYPOINT ["/app/entrypoint.sh"]

