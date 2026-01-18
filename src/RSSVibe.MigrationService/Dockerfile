# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level configuration files
COPY Directory.Build.props Directory.Packages.props global.json .editorconfig ./

# Copy all source projects (required for project references)
COPY src/ ./src/

# Restore dependencies
RUN dotnet restore src/RSSVibe.MigrationService/RSSVibe.MigrationService.csproj \
    -p:ContinuousIntegrationBuild=true

# Build the project
RUN dotnet build src/RSSVibe.MigrationService/RSSVibe.MigrationService.csproj \
    -c Release \
    --no-restore \
    -p:ContinuousIntegrationBuild=true

# Publish the application
RUN dotnet publish src/RSSVibe.MigrationService/RSSVibe.MigrationService.csproj \
    -c Release \
    --no-build \
    -o /app/publish \
    -p:ContinuousIntegrationBuild=true

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Run the migration service (one-shot execution)
ENTRYPOINT ["dotnet", "RSSVibe.MigrationService.dll"]
