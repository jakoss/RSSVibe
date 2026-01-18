# Stage 1: Build Blazor WASM
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level configuration files
COPY Directory.Build.props Directory.Packages.props global.json .editorconfig ./

# Copy all source projects (required for project references)
COPY src/ ./src/

# Restore dependencies
RUN dotnet restore src/RSSVibe.Client/RSSVibe.Client.csproj \
    -p:ContinuousIntegrationBuild=true

# Publish the Blazor WASM app
RUN dotnet publish src/RSSVibe.Client/RSSVibe.Client.csproj \
    -c Release \
    -o /app/publish \
    -p:ContinuousIntegrationBuild=true

# Stage 2: Serve with nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Copy published Blazor WASM files
COPY --from=build /app/publish/wwwroot .

# Copy nginx configuration
COPY src/RSSVibe.Client/nginx.conf /etc/nginx/nginx.conf

# Copy entrypoint script
COPY src/RSSVibe.Client/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Remove development appsettings (we'll generate production one at runtime)
RUN rm -f appsettings.Development.json* appsettings.json*

EXPOSE 80

# Use entrypoint script to generate config and start nginx
ENTRYPOINT ["/docker-entrypoint.sh"]
