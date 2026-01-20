# RSSVibe

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet) ![Blazor](https://img.shields.io/badge/Blazor-Interactive%20UI-5C2D91?logo=blazor) ![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

## Table of Contents
- [1. Project Name](#1-project-name)
- [2. Project Description](#2-project-description)
- [3. Tech Stack](#3-tech-stack)
- [4. Getting Started Locally](#4-getting-started-locally)
- [5. Docker Deployment](#5-docker-deployment)
- [6. Testing GitHub Actions Locally](#6-testing-github-actions-locally)
- [7. Available Scripts](#7-available-scripts)
- [8. Project Scope](#8-project-scope)
- [9. Project Status](#9-project-status)
- [10. License](#10-license)

## 1. Project Name
RSSVibe — AI-powered RSS feed generation for sites that no longer ship native feeds.

## 2. Project Description
RSSVibe automatically converts arbitrary websites into consumable RSS 2.0 feeds. An AI workflow inspects page structures, proposes CSS selectors, and lets users adjust configurations before scheduling background parsers that keep feeds fresh. The platform targets non-technical readers who need dependable subscriptions for blogs, gaming news, and other niche content. Core experiences include secure onboarding with ASP.NET Identity, AI-assisted feed creation, resilient parsing backed by Polly policies, telemetry with OpenTelemetry, and standards-compliant feed delivery at `/feed/{userId}/{feed_guid}`. Additional context and roadmap live in the [Product Requirements Document](.ai/prd.md).

## 3. Tech Stack
- **Runtime & Language:** .NET 10 with C# 14 (see `global.json`) across the solution.
- **Frontend:** Blazor using Fluent UI components with an `Interactive auto` render mode by default and `Server` mode where richer interactivity is required (`src/RSSVibe.Web`).
- **Backend API:** ASP.NET Core Minimal APIs with ASP.NET Identity for authentication and TUnit for testing (`src/RSSVibe.ApiService`).
- **Background Workloads:** TickerQ-driven scheduling executes periodic crawls and applies Polly policies (timeouts, retries, circuit breakers) for resiliency.
- **Persistence:** PostgreSQL 18 via Entity Framework Core 10 with separate models for draft analyses and approved feeds.
- **Caching:** Redis 8 combined with FusionCache implementing HybridCache patterns to balance in-memory and distributed caching.
- **AI Integration:** SemanticKernel (or Microsoft Agentic Framework) orchestrates selector discovery and partners with OpenRouter-hosted LLMs.
- **Orchestration & Defaults:** `.NET Aspire` (`src/RSSVibe.AppHost` and `src/RSSVibe.ServiceDefaults`) coordinates local dependencies, connection strings, and deployment scaffolding.
- **Telemetry:** OpenTelemetry captures crawl/parse timings, HTTP status distributions, retries, and circuit-breaker events.
- **Documentation:** Additional architectural details are available in the [Tech Stack Overview](.ai/tech-stack.md).

## 4. Getting Started Locally
### Prerequisites
- .NET SDK 10.0 (enforced by `global.json`).
- Docker Desktop or an equivalent container runtime for .NET Aspire assets, PostgreSQL, Redis, and Testcontainers.
- An OpenRouter API key for AI-powered analysis.
- Seed credentials for the bootstrapped root user (handled through environment variables).

### Clone & Restore
```bash
git clone <repository-url>
cd RSSVibe
dotnet restore
```

### Configure Secrets
- Export environment variables for the OpenRouter key (for example `OpenRouter__ApiKey`) and the initial ASP.NET Identity root account (email, password, and forced reset flag). The root user must change their password at first login.
- Provide PostgreSQL and Redis connection strings if you are not relying on the defaults provisioned by .NET Aspire.
- Consider using `dotnet user-secrets` for local development to avoid checking credentials into source control.

### Run the Stack
```bash
# Launch the Aspire AppHost to bring up the web frontend, API, and backing services.
dotnet run --project src/RSSVibe.AppHost
```
- The AppHost wires up the Blazor front end, the API service, PostgreSQL, Redis, and background worker infrastructure.
- On first launch, sign in with the bootstrapped root credentials, rotate the password, and configure a valid OpenRouter model.

### Development Workflow
- Submit a site URL through the AI-assisted flow, review proposed selectors, and approve the configuration to create a feed.
- Use the generated RSS URL `/feed/{userId}/{feed_guid}` in your preferred reader to validate output.

## 5. Docker Deployment

RSSVibe provides Docker images for all deployable components via GitHub Container Registry (ghcr.io).

### Available Images

- **API Service**: `ghcr.io/<username>/rssvibe-apiservice:latest`
- **Client (Frontend)**: `ghcr.io/<username>/rssvibe-client:latest`
- **Migration Service**: `ghcr.io/<username>/rssvibe-migrationservice:latest`

### Quick Start with Docker Compose

```bash
# Start the full stack
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the stack
docker-compose down
```

The application will be available at:
- **Frontend**: http://localhost
- **API**: http://localhost:8080

### Environment Variables

#### API Service
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string (required)
- `JwtSettings__Secret` - JWT signing key, min 32 characters (required)
- `JwtSettings__Issuer` - JWT issuer (default: "RSSVibe")
- `JwtSettings__Audience` - JWT audience (default: "RSSVibe")
- `JwtSettings__ExpirationMinutes` - Token expiration in minutes (default: 60)

#### Client (Frontend)
- `API_BASE_URL` - API base URL (required, e.g., `https://api.example.com`)

#### Migration Service
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string (required)

### Manual Docker Run

```bash
# Run migration (one-time)
docker run --rm \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=rssvibe;..." \
  ghcr.io/yourusername/rssvibe-migrationservice:latest

# Run API service
docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=rssvibe;..." \
  -e JwtSettings__Secret="your-secret-key-here" \
  ghcr.io/yourusername/rssvibe-apiservice:latest

# Run Client (requires API_BASE_URL)
docker run -d -p 80:80 \
  -e API_BASE_URL=http://localhost:8080 \
  ghcr.io/yourusername/rssvibe-client:latest
```

### Kubernetes Deployment

Example Kubernetes manifests:

```yaml
# Migration Job
apiVersion: batch/v1
kind: Job
metadata:
  name: rssvibe-migration
spec:
  template:
    spec:
      containers:
      - name: migration
        image: ghcr.io/yourusername/rssvibe-migrationservice:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: rssvibe-db
              key: connection-string
      restartPolicy: OnFailure

---
# API Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: rssvibe-api
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: api
        image: ghcr.io/yourusername/rssvibe-apiservice:latest
        ports:
        - containerPort: 8080
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: rssvibe-db
              key: connection-string
        livenessProbe:
          httpGet:
            path: /health
            port: 8080

---
# Client Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: rssvibe-client
spec:
  replicas: 2
  template:
    spec:
      containers:
      - name: client
        image: ghcr.io/yourusername/rssvibe-client:latest
        ports:
        - containerPort: 80
        env:
        - name: API_BASE_URL
          value: "https://api.rssvibe.com"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
```

## 6. Testing GitHub Actions Locally

This project uses [act](https://github.com/nektos/act) to test GitHub Actions workflows locally before pushing to GitHub.

### Prerequisites

Install act:

```bash
# macOS
brew install act

# Linux
curl https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash

# Windows
choco install act-cli
```

### Setup

1. Copy the example environment file:
   ```bash
   cp .github/.env.act.example .github/.env.act
   ```

2. Update `.github/.env.act` with your settings (optional for basic testing)

### Running Workflows Locally

```bash
# Test PR workflow (build, format, tests)
act pull_request -W .github/workflows/ci-pr.yml

# Test develop workflow (includes Docker builds)
act push -W .github/workflows/ci-cd-develop.yml

# Test specific job
act push -W .github/workflows/ci-cd-develop.yml -j validate

# List all workflows
act -l

# Dry run (show what would execute)
act pull_request --dry-run
```

### Limitations

- Docker images are built locally but not pushed to ghcr.io (requires real credentials)
- Matrix jobs run sequentially (not parallel like on GitHub)
- .NET 10 SDK may need to be installed in the act Docker image

See [act documentation](https://github.com/nektos/act) for more options.

## 7. Available Scripts
- `dotnet restore` — downloads NuGet dependencies for all projects in `RSSVibe.slnx`.
- `dotnet build -c Release -p:TreatWarningsAsErrors=true` — compiles the solution with analyzers enforced.
- `dotnet test` — executes TUnit-based unit and integration tests, leveraging Testcontainers when necessary.
- `dotnet format` — applies `.editorconfig` and analyzer formatting rules.
- `dotnet run --project src/RSSVibe.AppHost` — starts the Aspire-orchestrated development environment.

## 8. Project Scope
- **In Scope (MVP):** ASP.NET Identity login with root-user bootstrap, AI-driven CSS selector discovery and preview, configurable hourly-or-greater update schedules, resilient background parsing with HTTP caching, and standards-compliant RSS feeds exposing title/link/pubDate metadata.
- **Out of Scope (MVP):** Full article content extraction, advanced visual editing for selectors, support for JavaScript-rendered or paywalled sites beyond flagging, collaboration features, custom feed domains, and long-term retention or pruning policies.

## 9. Project Status
The project is in active MVP development. Success metrics target first-attempt AI selector accuracy of 60%, end-to-end feed creation in under five minutes, resilient background processing over at least seven days, and actionable telemetry across crawl, parse, and RSS generation spans.

## 10. License
Distributed under the MIT License. See `LICENSE` for details.
