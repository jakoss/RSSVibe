# Product Requirements Document (PRD) - RssVibe

## 1. Product Overview

RssVibe is an AI-powered web application designed to automatically convert any website into a subscribable RSS feed. It leverages advanced AI models to analyze website HTML structures, detect article patterns, and generate standard RSS 2.0 feeds without requiring technical expertise. The system is built with a Blazor frontend, an ASP.NET Core API backend, and PostgreSQL for data persistence. Features include automated AI-powered analysis, user-driven feed configuration, and scheduled parsing using a background worker with resiliency policies.

## 2. User Problem

Many websites have abandoned traditional RSS support in favor of social media platforms, leaving users without a reliable way to follow their favorite content. Non-technical users, especially those interested in gaming news, blogs, or niche sites, often face barriers when trying to create RSS feeds manually. RssVibe addresses this problem by eliminating the need for manual configuration, enabling users to effortlessly generate RSS feeds by simply providing a website URL.

## 3. Functional Requirements

1. User Authentication and Setup
   - Secure login using ASP.NET Identity with initial root user bootstrapped by environment variables.
   - Forced password change on first login of the bootstrapped root user for security.
   - System startup requires a valid OpenRouter API key and model configuration.

2. Feed Creation Flow
   - Users submit a website URL to initiate feed creation.
   - Pre-flight detection to flag JavaScript-rendered, authentication-required, or paywalled content.
   - AI-driven analysis of the website to propose CSS selectors identifying article containers.
   - Review of AI-suggested selectors with side-by-side preview of up to 10 items, where users can edit selectors if necessary.
   - Once approved, the system stores the selected strategy and creates a corresponding RSS feed.

3. Parsing and Scheduling
   - A single background worker periodically fetches and parses website content based on user-defined update frequencies (hourly minimum).
   - Implementation of HTTP caching (ETag/Last-Modified) and fallback TTL mechanisms.
   - Use of resiliency patterns (e.g., Polly policies for timeouts, retries, and circuit-breakers) to ensure reliable operation.

4. Extraction and Storage
   - Extract key article fields: title, link, and pubDate (if available).
   - Generate unique identifiers (UUIDv7) for feeds and items, and compute deterministic fingerprints for deduplication.
   - Maintain separate data models for draft analysis (FeedAnalysis) and approved feeds (Feed).

5. RSS Generation
   - Generate standard RSS 2.0 feeds with required metadata (title, description, language, lastBuildDate, atom self-link, ttl=60).
   - Public feed access via URL format: /feed/{userId}/{feed_guid}.

6. Telemetry and Resilience
   - Integration with OpenTelemetry for capturing operational metrics including crawl and parse durations, HTTP status distribution, retry counts, and circuit-breaker events.
   - Logging of key processing stages (CrawlStart, Parse, GenerateRSS) for monitoring purposes.

## 4. Product Boundaries

Included in MVP:
   - User authentication and secure login setup via ASP.NET Identity.
   - Website management allowing users to add, view, modify, and delete tracked websites.
   - AI-powered analysis to automatically detect content structures and generate CSS selectors with preview capabilities.
   - Automated RSS feed generation with basic fields (title, link, pubDate).
   - Configurable update schedules for each website.
   - Standard RSS delivery compatible with popular feed readers.

Excluded from MVP:
   - Full article content extraction beyond titles and links.
   - Visual highlighting or advanced editing UI for extracted content areas.
   - Advanced user features such as team collaboration, custom feed URLs, or per-article fetching.
   - Support for JavaScript-rendered or authentication-required websites (only flagged during pre-flight analysis).
   - Storage caps, extensive retention policies, and pruning mechanisms.

## 5. User Stories

### US-001: User Registration and Secure Login
- Title: Account Creation and Secure Login
- Description: As a new user, I want to register an account and log in securely so that my feeds are personalized and access is protected.
- Acceptance Criteria:
  1. User can create an account using ASP.NET Identity.
  2. Login must validate against the securely stored credentials and environment-provisioned root user.
  3. Appropriate error messages are displayed for invalid credentials.

### US-002: Submit Website for Feed Generation
- Title: Website Submission
- Description: As an authenticated user, I want to submit a website URL to generate an RSS feed so that I can follow content updates.
- Acceptance Criteria:
  1. The platform accepts a valid URL input.
  2. Pre-flight analysis is triggered to verify that the site is compatible (non-JS rendered, not behind authentication, etc.).
  3. User is informed promptly if the URL is unsuitable.

### US-003: AI-Powered Feed Analysis
- Title: Automated Content Detection
- Description: As a user, I want the system to automatically detect article containers and propose CSS selectors using AI so that I do not need to manually configure extraction rules.
- Acceptance Criteria:
  1. The AI analysis returns a selectable strategy with selectors for list, item, title, link, and (optional) pubDate.
  2. Any errors or warnings from the pre-flight or analysis phase are clearly presented.
  3. A preview of up to 10 extracted items is shown to the user.

### US-004: Edit and Approve AI Suggestions
- Title: Selector Customization and Approval
- Description: As a user, I want to edit the AI-suggested selectors if needed and approve the strategy to create my RSS feed.
- Acceptance Criteria:
  1. The user can modify each of the suggested selectors.
  2. A real-time preview updates with the modified selectors.
  3. On approval, the system saves the chosen configuration and creates the feed.

### US-005: Configure Feed Update Schedule
- Title: Schedule Configuration
- Description: As a user, I want to set how frequently my feed is updated so that I can balance timeliness with resource usage.
- Acceptance Criteria:
  1. The user is allowed to select between hourly, daily, and weekly update intervals.
  2. The system enforces a minimum update frequency of once per hour.
  3. The configuration is stored and triggers scheduled parsing by the background worker.

### US-006: Public Feed Access
- Title: RSS Feed Delivery
- Description: As a user, I want to access my generated RSS feed via a public URL so that I can subscribe to it using my feed reader.
- Acceptance Criteria:
  1. The RSS feed is publicly accessible in the format /feed/{userId}/{feed_guid}.
  2. The feed conforms to standard RSS 2.0 specifications.
  3. The feed updates automatically based on the scheduled frequency.

## 6. Success Metrics

1. Functional Success:
   - RSS feeds are generated successfully for at least 3 different website structures with a first-attempt accuracy of 60% for AI detection.
   - Users can create a working feed in under 5 minutes from URL submission to feed generation.
   - Feeds update reliably for a minimum of 7 days without manual intervention.

2. Quality Metrics:
   - The preview functionality consistently aligns with the actual feed content, ensuring no duplicate entries through effective fingerprinting.
   - Error handling and pre-flight detection accurately identify incompatible websites.

3. Operational Metrics:
   - Background parsing processes run with a default concurrency of 4 while maintaining stable performance under load.
   - Polly-based resiliency policies (timeouts, retries, circuit-breaker) effectively prevent system crashes and manage transient errors.
   - Telemetry spans (CrawlStart, Parse, GenerateRSS) provide actionable insights into system performance and reliability.
