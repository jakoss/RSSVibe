# RSSVibe

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet) ![Blazor](https://img.shields.io/badge/Blazor-Interactive%20UI-5C2D91?logo=blazor) ![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

## Table of Contents
- [1. Project Name](#1-project-name)
- [2. Project Description](#2-project-description)
- [3. Tech Stack](#3-tech-stack)
- [4. Getting Started Locally](#4-getting-started-locally)
- [5. Available Scripts](#5-available-scripts)
- [6. Project Scope](#6-project-scope)
- [7. Project Status](#7-project-status)
- [8. License](#8-license)

## 1. Project Name
RSSVibe — AI-powered RSS feed generation for sites that no longer ship native feeds.

## 2. Project Description
RSSVibe automatically converts arbitrary websites into consumable RSS 2.0 feeds. An AI workflow inspects page structures, proposes CSS selectors, and lets users adjust configurations before scheduling background parsers that keep feeds fresh. The platform targets non-technical readers who need dependable subscriptions for blogs, gaming news, and other niche content. Core experiences include secure onboarding with ASP.NET Identity, AI-assisted feed creation, resilient parsing backed by Polly policies, telemetry with OpenTelemetry, and standards-compliant feed delivery at `/feed/{userId}/{feed_guid}`. Additional context and roadmap live in the [Product Requirements Document](.ai/prd.md).

## 3. Tech Stack
- **Runtime & Language:** .NET 9 with C# 13 (see `global.json`) across the solution.
- **Frontend:** Blazor using Fluent UI components with an `Interactive auto` render mode by default and `Server` mode where richer interactivity is required (`src/RSSVibe.Web`).
- **Backend API:** ASP.NET Core Minimal APIs with ASP.NET Identity for authentication and TUnit for testing (`src/RSSVibe.ApiService`).
- **Background Workloads:** TickerQ-driven scheduling executes periodic crawls and applies Polly policies (timeouts, retries, circuit breakers) for resiliency.
- **Persistence:** PostgreSQL 18 via Entity Framework Core 9 with separate models for draft analyses and approved feeds.
- **Caching:** Redis 8 combined with FusionCache implementing HybridCache patterns to balance in-memory and distributed caching.
- **AI Integration:** SemanticKernel (or Microsoft Agentic Framework) orchestrates selector discovery and partners with OpenRouter-hosted LLMs.
- **Orchestration & Defaults:** `.NET Aspire` (`src/RSSVibe.AppHost` and `src/RSSVibe.ServiceDefaults`) coordinates local dependencies, connection strings, and deployment scaffolding.
- **Telemetry:** OpenTelemetry captures crawl/parse timings, HTTP status distributions, retries, and circuit-breaker events.
- **Documentation:** Additional architectural details are available in the [Tech Stack Overview](.ai/tech-stack.md).

## 4. Getting Started Locally
### Prerequisites
- .NET SDK 9.0 (enforced by `global.json`).
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

## 5. Available Scripts
- `dotnet restore` — downloads NuGet dependencies for all projects in `RSSVibe.slnx`.
- `dotnet build -c Release -p:TreatWarningsAsErrors=true` — compiles the solution with analyzers enforced.
- `dotnet test` — executes TUnit-based unit and integration tests, leveraging Testcontainers when necessary.
- `dotnet format` — applies `.editorconfig` and analyzer formatting rules.
- `dotnet run --project src/RSSVibe.AppHost` — starts the Aspire-orchestrated development environment.

## 6. Project Scope
- **In Scope (MVP):** ASP.NET Identity login with root-user bootstrap, AI-driven CSS selector discovery and preview, configurable hourly-or-greater update schedules, resilient background parsing with HTTP caching, and standards-compliant RSS feeds exposing title/link/pubDate metadata.
- **Out of Scope (MVP):** Full article content extraction, advanced visual editing for selectors, support for JavaScript-rendered or paywalled sites beyond flagging, collaboration features, custom feed domains, and long-term retention or pruning policies.

## 7. Project Status
The project is in active MVP development. Success metrics target first-attempt AI selector accuracy of 60%, end-to-end feed creation in under five minutes, resilient background processing over at least seven days, and actionable telemetry across crawl, parse, and RSS generation spans.

## 8. License
Distributed under the MIT License. See `LICENSE` for details.
