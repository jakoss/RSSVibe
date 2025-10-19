# REST API Plan

## 1. Resources
- `Auth` → `AspNetUsers` and related ASP.NET Identity tables; manages user credentials, forced password resets, and token issuance.
- `FeedAnalysis` → `FeedAnalyses`; tracks AI-driven crawl readiness, selector proposals, warnings, and approval linkage.
- `Feed` → `Feeds`; represents approved RSS configurations, scheduling preferences, and delivery metadata.
- `FeedItem` → `FeedItems`; immutable snapshot of extracted articles tied to a feed and parse runs.
- `FeedParseRun` → `FeedParseRuns`; captures periodic parser execution metrics, HTTP cache state, and failure diagnostics.
- `FeedParseRunItem` → `FeedParseRunItems`; change-tracking join table between runs and items for audit and preview flows.
- `PublicRss` → Virtual resource generated from feeds and items, delivered as RSS 2.0 XML for anonymous consumption.

## 2. Endpoints
All authenticated API endpoints are mapped beneath a shared Minimal API group configured as `var v1 = app.MapGroup("/api/v1");` so we can centralize filters (validation, auth, error handling) and preserve a clean surface for future `/api/v2` breaking changes.

### 2.1 Auth

#### POST /api/v1/auth/register
- Description: Create a new user account backed by ASP.NET Identity; disabled in production when provisioning root user via environment variables.
- Request:
```json
{
  "email": "user@example.com",
  "password": "Passw0rd!",
  "displayName": "Jane Doe",
  "mustChangePassword": false
}
```
- Response:
```json
{
  "userId": "uuid",
  "email": "user@example.com",
  "displayName": "Jane Doe",
  "mustChangePassword": false
}
```
- Success: `201 Created` with Location `/api/v1/auth/profile`.
- Errors: `400` (validation), `409` (email already registered), `503` (identity store unavailable).

#### POST /api/v1/auth/login
- Description: Authenticate and return JWT access token plus refresh token; flag root user if password rotation required.
- Request:
```json
{
  "email": "user@example.com",
  "password": "Passw0rd!",
  "rememberMe": true
}
```
- Response:
```json
{
  "accessToken": "jwt",
  "refreshToken": "guid",
  "expiresInSeconds": 3600,
  "mustChangePassword": true
}
```
- Success: `200 OK`.
- Errors: `400` (missing credentials), `401` (invalid credentials), `423` (account locked).

#### POST /api/v1/auth/refresh
- Description: Exchange refresh token for new JWT; revoke on reuse attempts.
- Request:
```json
{
  "refreshToken": "guid"
}
```
- Response:
```json
{
  "accessToken": "jwt",
  "refreshToken": "guid",
  "expiresInSeconds": 3600,
  "mustChangePassword": false
}
```
- Success: `200 OK`.
- Errors: `400` (invalid payload), `401` (expired or revoked token), `409` (token replay detected).

#### POST /api/v1/auth/change-password
- Description: Force password rotation, required for bootstrapped root user on first login.
- Request:
```json
{
  "currentPassword": "OldPass!",
  "newPassword": "NewPass!2"
}
```
- Response: `204 No Content`.
- Errors: `400` (weak password), `401` (invalid current password), `429` (too many attempts).

#### GET /api/v1/auth/profile
- Description: Retrieve current user profile including security posture metadata (e.g., password rotation requirement).
- Query parameters: none.
- Response:
```json
{
  "userId": "uuid",
  "email": "user@example.com",
  "displayName": "Jane Doe",
  "roles": ["User"],
  "mustChangePassword": false,
  "createdAt": "2024-05-01T12:01:00Z"
}
```
- Success: `200 OK`.
- Errors: `401` (unauthenticated), `503` (identity store unavailable).

### 2.2 Feed Analyses

#### POST /api/v1/feed-analyses
- Description: Initiate AI-powered analysis and preflight checks for a submitted URL; enqueues background workflow and returns analysis resource.
- Request:
```json
{
  "targetUrl": "https://example.com/news",
  "aiModel": "openrouter/gpt-4.1-mini",
  "forceReanalysis": false
}
```
- Response:
```json
{
  "analysisId": "uuid",
  "status": "pending",
  "normalizedUrl": "https://example.com/news",
  "preflight": {
    "checks": ["RequiresJavascript"],
    "details": { "requiresAuthentication": false }
  },
  "createdAt": "2024-05-01T12:01:00Z"
}
```
- Success: `202 Accepted` with Location `/api/v1/feed-analyses/{analysisId}`.
- Errors: `400` (invalid URL, missing OpenRouter configuration), `401` (unauthenticated), `409` (duplicate normalized URL per user), `422` (preflight fails validation), `503` (AI provider unavailable).

#### GET /api/v1/feed-analyses
- Description: List analyses for current user, supporting dashboards and moderation flows.
- Query parameters: `status` (`pending|completed|failed|superseded`), `sort` (`createdAt|updatedAt` with suffix `:asc|:desc` default `createdAt:desc`), `skip` (>=0 default 0), `take` (1-50 default 20), `search` (partial target URL).
- Response:
```json
{
  "items": [
    {
      "analysisId": "uuid",
      "targetUrl": "https://example.com/news",
      "status": "completed",
      "warnings": ["Possible paywall"],
      "analysisStartedAt": "2024-05-01T12:01:00Z",
      "analysisCompletedAt": "2024-05-01T12:02:10Z"
    }
  ],
  "paging": {
    "skip": 0,
    "take": 20,
    "totalCount": 45,
    "hasMore": true
  }
}
```
- Success: `200 OK`.
- Errors: `401`, `422` (bad sort or offset input).

#### GET /api/v1/feed-analyses/{analysisId}
- Description: Retrieve full analysis payload including selectors, warnings, and preflight metadata.
- Response:
```json
{
  "analysisId": "uuid",
  "targetUrl": "https://example.com/news",
  "normalizedUrl": "https://example.com/news",
  "status": "completed",
  "preflightChecks": ["RequiresJavascript"],
  "preflightDetails": {},
  "selectors": {
    "list": ".article-list",
    "item": ".article",
    "title": ".article-title",
    "link": ".article-link",
    "published": ".article-date"
  },
  "warnings": ["RequiresJavascript"],
  "aiModel": "openrouter/gpt-4.1-mini",
  "approvedFeedId": "uuid",
  "createdAt": "2024-05-01T12:01:00Z",
  "updatedAt": "2024-05-01T12:03:00Z"
}
```
- Success: `200 OK`.
- Errors: `401`, `403` (accessing other user’s record), `404`.

#### PATCH /api/v1/feed-analyses/{analysisId}/selectors
- Description: Allow user to update proposed selectors before approving the feed; persists strongly typed `FeedSelectors`.
- Request:
```json
{
  "selectors": {
    "list": ".articles",
    "item": ".article",
    "title": ".title",
    "link": ".title a",
    "published": ".date"
  }
}
```
- Response:
```json
{
  "analysisId": "uuid",
  "targetUrl": "https://example.com/news",
  "normalizedUrl": "https://example.com/news",
  "status": "completed",
  "preflightChecks": ["RequiresJavascript"],
  "preflightDetails": {},
  "selectors": {
    "list": ".articles",
    "item": ".article",
    "title": ".title",
    "link": ".title a",
    "published": ".date"
  },
  "warnings": ["RequiresJavascript"],
  "aiModel": "openrouter/gpt-4.1-mini",
  "approvedFeedId": "uuid",
  "createdAt": "2024-05-01T12:01:00Z",
  "updatedAt": "2024-05-01T12:05:00Z"
}
```
- Errors: `400` (invalid selector schema), `401`, `403`, `404`, `409` (analysis already superseded).

#### POST /api/v1/feed-analyses/{analysisId}/rerun
- Description: Re-enqueue AI analysis after selector edits or failures.
- Response:
```json
{
  "analysisId": "uuid",
  "status": "pending"
}
```
- Success: `202 Accepted`.
- Errors: `401`, `403`, `404`, `409` (analysis already linked to approved feed), `429` (rerun throttle).

#### GET /api/v1/feed-analyses/{analysisId}/preview
- Description: Return up to 10 preview items captured using current selectors for side-by-side UI.
- Query parameters: `limit` (1-10 default 10), `fresh` (boolean to bypass cache).
- Response:
```json
{
  "analysisId": "uuid",
  "items": [
    {
      "title": "Article 1",
      "link": "https://example.com/news/article-1",
      "publishedAt": "2024-05-01T10:00:00Z",
      "rawHtmlExcerpt": "<article>...</article>"
    }
  ]
}
```
- Success: `200 OK`.
- Errors: `401`, `403`, `404`, `409` (analysis not completed), `503` (preview crawler unavailable).

### 2.3 Feeds

#### POST /api/v1/feeds
- Description: Approve an analysis and create a feed configuration; copies selectors, schedule, and metadata into `Feeds`.
- Request:
```json
{
  "analysisId": "uuid",
  "title": "Example News",
  "description": "Latest updates from Example",
  "language": "en",
  "updateInterval": {
    "unit": "hour",
    "value": 1
  },
  "ttlMinutes": 60,
  "selectorsOverride": null
}
```
- Response:
```json
{
  "feedId": "uuid",
  "userId": "uuid",
  "sourceUrl": "https://example.com/news",
  "title": "Example News",
  "language": "en",
  "updateInterval": {
    "unit": "hour",
    "value": 1
  },
  "ttlMinutes": 60,
  "lastParsedAt": null,
  "nextParseAfter": "2024-05-01T13:00:00Z",
  "rssUrl": "/feed/{userId}/{feedId}"
}
```
- Success: `201 Created` with Location `/api/v1/feeds/{feedId}`.
- Errors: `400` (missing schedule or violates constraints), `401`, `403`, `404` (analysis not found), `409` (duplicate normalized source URL), `422` (analysis not completed or preflight failed), `503` (background scheduler unavailable).

#### GET /api/v1/feeds
- Description: List authenticated user feeds with offset pagination, filtering, and schedule insights.
- Query parameters: `skip` (>=0 default 0), `take` (1-50 default 20), `sort` (`createdAt|lastParsedAt|title` with `:asc|:desc` default `lastParsedAt:desc`), `status` (`lastParseStatus` filter), `nextParseBefore` (ISO timestamp), `search` (title or URL), `includeInactive` (boolean).
- Response:
```json
{
  "items": [
    {
      "feedId": "uuid",
      "userId": "uuid",
      "sourceUrl": "https://example.com/news",
      "normalizedSourceUrl": "https://example.com/news",
      "title": "Example News",
      "description": "Latest updates from Example",
      "language": "en",
      "updateInterval": {
        "unit": "hour",
        "value": 1
      },
      "ttlMinutes": 60,
      "etag": "\"abc\"",
      "lastModified": "2024-05-01T12:00:00Z",
      "lastParsedAt": "2024-05-01T12:30:00Z",
      "nextParseAfter": "2024-05-01T13:30:00Z",
      "lastParseStatus": "succeeded",
      "pendingParseCount": 0,
      "analysisId": "uuid",
      "createdAt": "2024-05-01T11:00:00Z",
      "updatedAt": "2024-05-01T12:30:05Z",
      "rssUrl": "/feed/{userId}/{feedId}"
    }
  ],
  "paging": {
    "skip": 0,
    "take": 20,
    "totalCount": 12,
    "hasMore": false
  }
}
```
- Success: `200 OK`.
- Errors: `401`, `422` (invalid filters).

#### GET /api/v1/feeds/{feedId}
- Description: Retrieve feed detail including selectors, schedule, and cache metadata.
- Response:
```json
{
  "feedId": "uuid",
  "userId": "uuid",
  "sourceUrl": "https://example.com/news",
  "normalizedSourceUrl": "https://example.com/news",
  "title": "Example News",
  "description": "Latest updates from Example",
  "language": "en",
  "selectors": {
    "list": ".article-list",
    "item": ".article",
    "title": ".article-title",
    "link": ".article-link",
    "published": ".article-date"
  },
  "updateInterval": {
    "unit": "hour",
    "value": 1
  },
  "ttlMinutes": 60,
  "etag": "\"abc\"",
  "lastModified": "2024-05-01T12:00:00Z",
  "lastParsedAt": "2024-05-01T12:30:00Z",
  "nextParseAfter": "2024-05-01T13:30:00Z",
  "lastParseStatus": "succeeded",
  "analysisId": "uuid",
  "createdAt": "2024-05-01T11:00:00Z",
  "updatedAt": "2024-05-01T12:30:05Z",
  "rssUrl": "/feed/{userId}/{feedId}",
  "cacheHeaders": {
    "cacheControl": "public, max-age=300",
    "etag": "\"abc\"",
    "lastModified": "Wed, 01 May 2024 12:00:00 GMT"
  }
}
```
- Success: `200 OK`.
- Errors: `401`, `403`, `404`.

#### PATCH /api/v1/feeds/{feedId}
- Description: Update mutable fields (title, description, language, selectors override, schedule, ttl).
- Request:
```json
{
  "title": "Example News - Updated",
  "description": "Curated updates",
  "language": "en",
  "updateInterval": {
    "unit": "hour",
    "value": 2
  },
  "ttlMinutes": 90,
  "selectorsOverride": {
    "list": ".articles",
    "item": ".article",
    "title": ".title",
    "link": ".title a",
    "published": ".date"
  }
}
```
- Success: `200 OK`.
- Errors: `400`, `401`, `403`, `404`, `409` (normalized source URL conflict), `422` (invalid selector schema or schedule), `503` (scheduler unavailable).

#### DELETE /api/v1/feeds/{feedId}
- Description: Soft delete feed, cascade stops to scheduler and optional purge of items.
- Response: `204 No Content`.
- Errors: `401`, `403`, `404`, `409` (feed currently parsing), `503` (scheduler unavailable).

#### POST /api/v1/feeds/{feedId}/trigger-parse
- Description: Manually enqueue immediate parse run; limited by rate limiter and Polly policy.
- Response:
```json
{
  "feedId": "uuid",
  "parseRunId": "uuid",
  "status": "scheduled"
}
```
- Success: `202 Accepted`.
- Errors: `401`, `403`, `404`, `409` (parse already queued), `429` (manual trigger cooldown), `503` (parser offline).

### 2.4 Feed Items

#### GET /api/v1/feeds/{feedId}/items
- Description: Offset-paginated retrieval of feed items using `AsNoTracking()` and indexed sorting.
- Query parameters: `skip` (>=0 default 0), `take` (1-100 default 25), `sort` (`publishedAt|discoveredAt|lastSeenAt` with `:desc` default `publishedAt:desc`), `since` (ISO timestamp filter on `PublishedAt`), `until`, `changeKind` (`new|refreshed|unchanged`), `includeMetadata` (boolean to include `rawMetadata`).
- Response:
```json
{
  "items": [
    {
      "itemId": "uuid",
      "feedId": "uuid",
      "title": "Article 1",
      "summary": "Summary text",
      "link": "https://example.com/news/article-1",
      "sourceUrl": "https://example.com/news/article-1",
      "normalizedSourceUrl": "https://example.com/news/article-1",
      "publishedAt": "2024-05-01T10:00:00Z",
      "discoveredAt": "2024-05-01T10:05:00Z",
      "lastSeenAt": "2024-05-01T10:05:00Z",
      "changeKind": "new"
    }
  ],
  "paging": {
    "skip": 0,
    "take": 25,
    "totalCount": 240,
    "hasMore": true
  }
}
```
- Success: `200 OK` with `ETag` and `Cache-Control: private, max-age=60`.
- Errors: `401`, `403`, `404`, `422` (invalid sort), `503` (database timeout).

#### GET /api/v1/feeds/{feedId}/items/{itemId}
- Description: Detailed view of a feed item including associated parse runs.
- Response:
```json
{
  "itemId": "uuid",
  "feedId": "uuid",
  "title": "Article 1",
  "summary": "Summary text",
  "link": "https://example.com/news/article-1",
  "sourceUrl": "https://example.com/news/article-1",
  "normalizedSourceUrl": "https://example.com/news/article-1",
  "publishedAt": "2024-05-01T10:00:00Z",
  "discoveredAt": "2024-05-01T10:05:00Z",
  "lastSeenAt": "2024-05-01T10:05:00Z",
  "firstParseRunId": "uuid",
  "lastParseRunId": "uuid",
  "rawMetadata": {
    "author": "Example Author",
    "tags": ["news", "example"],
    "wordCount": 525
  },
  "createdAt": "2024-05-01T10:05:00Z",
  "updatedAt": "2024-05-01T10:05:00Z"
}
```
- Success: `200 OK`.
- Errors: `401`, `403`, `404`.

### 2.5 Feed Parse Runs

#### GET /api/v1/feeds/{feedId}/parse-runs
- Description: Offset-paginated history of parse runs ordered by `StartedAt desc` using indexed query.
- Query parameters: `skip` (>=0 default 0), `take` (1-50 default 20), `status`, `from`/`to` (ISO interval on `StartedAt`), `includeFailuresOnly` (boolean), `includeResponseHeaders` (boolean).
- Response:
```json
{
  "runs": [
    {
      "parseRunId": "uuid",
      "feedId": "uuid",
      "startedAt": "2024-05-01T12:30:00Z",
      "completedAt": "2024-05-01T12:30:08Z",
      "status": "succeeded",
      "failureReason": null,
      "httpStatusCode": 200,
      "responseHeaders": {
        "etag": "\"abc\"",
        "lastModified": "Wed, 01 May 2024 12:00:00 GMT"
      },
      "fetchedItemsCount": 25,
      "newItemsCount": 5,
      "updatedItemsCount": 3,
      "skippedItemsCount": 17
    }
  ],
  "paging": {
    "skip": 0,
    "take": 20,
    "totalCount": 42,
    "hasMore": true
  }
}
```
- Success: `200 OK`.
- Errors: `401`, `403`, `404`, `422`.

#### GET /api/v1/feeds/{feedId}/parse-runs/{runId}
- Description: Retrieve run details including resiliency metrics and HTTP caching info.
- Response:
```json
{
  "parseRunId": "uuid",
  "feedId": "uuid",
  "startedAt": "2024-05-01T11:00:00Z",
  "completedAt": "2024-05-01T11:00:10Z",
  "status": "succeeded",
  "failureReason": null,
  "httpStatusCode": 200,
  "responseHeaders": {
    "etag": "\"abc\"",
    "lastModified": "Wed, 01 May 2024 10:59:59 GMT"
  },
  "fetchedItemsCount": 25,
  "newItemsCount": 5,
  "updatedItemsCount": 3,
  "skippedItemsCount": 17,
  "retryCount": 0,
  "circuitBreakerOpen": false,
  "createdAt": "2024-05-01T11:00:00Z"
}
```
- Success: `200 OK`.
- Errors: `401`, `403`, `404`.

#### GET /api/v1/feeds/{feedId}/parse-runs/{runId}/items
- Description: List items associated with a parse run using `FeedParseRunItems`.
- Query parameters: `changeKind`, `skip` (>=0 default 0), `take` (1-100 default 25).
- Response:
```json
{
  "items": [
    {
      "itemId": "uuid",
      "feedId": "uuid",
      "changeKind": "new",
      "seenAt": "2024-05-01T11:00:05Z",
      "title": "Article 1",
      "link": "https://example.com/news/article-1"
    }
  ],
  "paging": {
    "skip": 0,
    "take": 25,
    "totalCount": 25,
    "hasMore": false
  }
}
```
- Success: `200 OK`.
- Errors: `401`, `403`, `404`, `422`.

### 2.6 Health Checks

#### GET /health/live
- Description: ASP.NET Core liveness endpoint surfaced via built-in health checks middleware; verifies app is responsive without hitting external dependencies.
- Response:
```json
{
  "status": "Healthy"
}
```
- Success: `200 OK`.
- Errors: `503` (process unhealthy).

#### GET /health/ready
- Description: ASP.NET Core readiness endpoint aggregating database, Redis, OpenRouter, scheduler, and background worker queues via custom health check registrations.
- Response:
```json
{
  "status": "Degraded",
  "results": {
    "database": { "status": "Healthy", "duration": "00:00:00.0120000" },
    "redis": { "status": "Healthy", "duration": "00:00:00.0040000" },
    "openRouter": { "status": "Degraded", "duration": "00:00:00.3200000", "description": "Increased latency" },
    "scheduler": { "status": "Healthy", "duration": "00:00:00.0060000" }
  }
}
```
- Success: `200 OK` when all checks healthy, `503` when any dependency unhealthy.

### 2.7 Public RSS Delivery

#### GET /feed/{userId}/{feedId}
- Description: Anonymous endpoint returning RSS 2.0 XML for given feed; enforces TTL and HTTP caching semantics.
- Query parameters: optionally `format=xml` (default) reserved for future JSON support.
- Response: RSS XML with metadata (`title`, `description`, `language`, `lastBuildDate`, atom self-link, `<ttl>`), items sorted by `PublishedAt`.
- Headers: `Cache-Control: public, max-age=300`, `ETag`, `Last-Modified`.
- Success: `200 OK` or `304 Not Modified`.
- Errors: `404` (feed not found or deleted), `410` (feed retired), `429` (rate limit), `503` (parser backlog).

#### HEAD /feed/{userId}/{feedId}
- Description: Lightweight cache validation endpoint for feed readers.
- Success: `200 OK` with headers only.
- Errors mirror GET.

## 3. Authentication and Authorization
- Use ASP.NET Identity with password hashing (PBKDF2) and account lockout policies. Registration is optional; production provisioning relies on environment-seeded root user requiring forced password change.
- Issue JWT access tokens (15–30 minutes) and sliding refresh tokens stored server-side; tokens carry `sub`, `email`, `roles`, `mustChangePassword`. Require HTTPS and secure cookie storage for refresh tokens in browser clients.
- Enforce role-based policies: `Admin` for system diagnostics and other operational endpoints, `User` for feed operations. Resource-level authorization ensures `FeedAnalysis`, `Feed`, `FeedItem`, and `FeedParseRun` are accessible only to owners via `UserId` checks.
- Implement rate limiting (e.g., ASP.NET rate limiter) for `POST /api/v1/feed-analyses`, manual parse triggers, and login attempts to mitigate abuse.
- All APIs require bearer tokens except `/api/v1/auth/login`, `/api/v1/auth/register` (configurable), `/feed/*`, and `/health/*` (restricted by network ACLs or API gateway).
- Employ FusionCache backed by Redis for token blacklists and analysis preview caching while maintaining sliding expiration to reduce AI recomputation.
- Log OpenTelemetry traces for auth events, parse workflows, and AI analysis using W3C trace context to correlate with background worker spans.

## 4. Validation and Business Logic
- `Auth`:
  - Enforce strong password policy (length ≥12, complexity) and email format validation.
  - Reject registration when environment root user flag disables public signup.
  - On login, if `mustChangePassword` flag set in `AspNetUsers`, return `403` for all feed endpoints until password rotation occurs.
- `FeedAnalysis`:
  - Validate `targetUrl` as absolute HTTPS URL and normalize before persistence to honor `(UserId, NormalizedUrl)` unique index; respond `409` on duplicates.
  - Guard `forceReanalysis` and rerun endpoints with 15-minute cooldown per analysis to prevent AI thrashing; persist timestamp for throttling.
  - Restrict `PreflightChecks` bitmask to known enum values; reject unknown bits with `400`.
  - Persist strongly-typed `PreflightDetails` and `Selectors` matching schema (`list`, `item`, `title`, `link`, optional `published`, optional `summary`).
  - When analysis fails, capture failure reason and expose via response; require explicit rerun.
- `Feed`:
  - Ensure `title` length ≤200 characters, `description` ≤2000, `language` ≤16 and matches ISO 639 pattern.
  - Validate schedule: `unit ∈ {hour, day, week}` with `value ≥1` and compute derived minimum interval ≥1 hour; reject combos violating hourly minimum (e.g., `unit=day`, `value=0`).
  - Enforce `ttlMinutes ≥15`; default to 60 if omitted. Update parser TTL and RSS `<ttl>` simultaneously.
  - Deny feed creation when linked analysis `status != completed` or contains blocking preflight flags (`RequiresJavascript`, `RequiresAuthentication`, `Paywalled`). Provide override mechanism only for Admins.
  - Maintain `NormalizedSourceUrl` uniqueness per user; respond `409` on conflict.
  - Auto-populate `NextParseAfter` using schedule; background worker updates after each run.
  - When selectors override provided, validate structure identical to analysis schema and persist as `FeedSelectors`.
- `FeedItem`:
  - Read endpoints default to `AsNoTracking()` and filter by `FeedId`; optional include for metadata toggles to protect large payloads.
  - Sorting uses indexes (`PublishedAt`, `LastSeenAt`); reject unsupported sorts to avoid table scans.
  - Provide `If-None-Match` responses to minimize payload when no changes since `LastSeenAt`.
- `FeedParseRun`:
  - Manual trigger respects Polly policy: fail fast if circuit breaker open; return `409` with breaker status.
  - When retrieving runs, optionally include failure detail only for runs with `Status = failed`; ensure sensitive error messages sanitized.
  - For `GET /parse-runs/{runId}/items`, limit `take` ≤100 to cap join complexity; queries leverage composite indexes.
- `PublicRss`:
  - Validate `feedId` belongs to active feed; if feed deleted or owner disabled, return `404` or `410`.
  - Generate RSS using cached feed metadata; compute `lastBuildDate` from newest `FeedItem`. Respect HTTP conditional headers for efficient CDN caching.
  - Apply IP-based rate limiting (e.g., 1000 req/hour) and require TLS even for public feeds. Strip private metadata from items (no `rawMetadata`).
- Cross-cutting:
  - All list endpoints support offset pagination via `skip`/`take`; consider adding cursor-based pagination later for large datasets.
  - Validation failures return RFC 7807 problem details with error codes (`validation_error`, `schedule_conflict`, `duplicate_resource`).
  - Health check endpoints leverage ASP.NET `AddHealthChecks` with additional checks for PostgreSQL, Redis, OpenRouter, and scheduler queues; production deployments should expose `/health/live` publicly and gate `/health/ready` behind infrastructure auth.
  - Exceptions handled by global middleware returning consistent problem payload with correlation ID header `x-correlation-id`.
