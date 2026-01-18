# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level configuration files
COPY Directory.Build.props Directory.Packages.props global.json ./

# Copy all source projects (required for project references)
COPY src/ ./src/

# Restore dependencies
RUN dotnet restore src/RSSVibe.ApiService/RSSVibe.ApiService.csproj \
    -p:ContinuousIntegrationBuild=true

# Build the project
RUN dotnet build src/RSSVibe.ApiService/RSSVibe.ApiService.csproj \
    -c Release \
    --no-restore \
    -p:ContinuousIntegrationBuild=true

# Publish the application
RUN dotnet publish src/RSSVibe.ApiService/RSSVibe.ApiService.csproj \
    -c Release \
    --no-build \
    -o /app/publish \
    -p:ContinuousIntegrationBuild=true

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose HTTP and HTTPS ports
EXPOSE 8080
EXPOSE 8443

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "RSSVibe.ApiService.dll"]
