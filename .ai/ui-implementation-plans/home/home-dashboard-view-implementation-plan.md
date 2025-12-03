# View Implementation Plan: Home Dashboard

> **IMPORTANT**: This view MUST use typed API clients (`ApiClient.Feeds`, `ApiClient.FeedAnalyses`) for all API calls.  
> **NEVER** use raw `HttpClient.GetAsync()` or `HttpClient.PostAsJsonAsync()`.  
> See Section 7 (API Integration) for correct usage patterns.

## 1. Overview

The Home Dashboard serves as the primary landing page for authenticated users of RSSVibe. It provides an at-a-glance overview of the user's feed ecosystem, displaying key metrics, recent activity, and quick access to common workflows. The dashboard aggregates data from multiple sources (feeds and feed analyses) to give users immediate insight into their RSS feed monitoring status, highlighting feeds requiring attention and recent content updates.

**Key Purposes:**
- Display summary statistics of the user's feeds (total, active, failing)
- Show recent feed items across all feeds
- Highlight pending feed analyses awaiting approval
- Provide quick action buttons for common tasks
- Alert users to feeds requiring attention (failed parses, warnings)

## 2. View Routing

**Path:** `/`

**Route Protection:**
- Requires authentication via `<AuthorizeView>`
- Redirects unauthenticated users to `/login` using `<RedirectToLogin />` component
- Must respect forced password change guard (redirect to `/change-password` if `mustChangePassword: true`)

## 3. Component Structure

```
Home.razor (main page component)
├── StatisticsCardsSection
│   ├── StatisticCard (Total Feeds)
│   ├── StatisticCard (Active Feeds)
│   ├── StatisticCard (Failing Feeds)
│   └── StatisticCard (Pending Analyses)
├── RecentActivitySection
│   └── RecentFeedItemsList
│       └── FeedItemCard (multiple instances)
├── PendingAnalysesSection (conditional)
│   └── PendingAnalysisCard (multiple instances)
└── QuickActionsSection
    ├── CreateAnalysisButton
    ├── ViewFeedsButton
    └── ViewAnalysesButton
```

**Layout Integration:**
- Rendered within `MainLayout.razor` with navigation drawer and app bar
- No breadcrumbs displayed (top-level page)
- Uses MudBlazor grid system (`MudGrid`, `MudItem`) for responsive layout

## 4. Component Details

### Home.razor (Main Page Component)

**Component Description:**
The root page component that orchestrates the Home Dashboard. It fetches data from feeds and analyses endpoints, calculates statistics, and renders child components. Manages loading states, error handling, and data refresh logic.

**Main Elements:**
- `<AuthorizeView>` wrapper for authentication enforcement
- `<MudContainer MaxWidth="MaxWidth.ExtraLarge">` for content width constraint
- `<MudGrid>` for responsive layout with spacing
- Conditional rendering based on loading/error/empty states
- Loading skeleton placeholders during initial data fetch

**Handled Interactions:**
- Initial page load triggers data fetch
- Pull-to-refresh or manual refresh action (optional)
- Navigation to child views via quick action buttons
- Click on statistic cards to navigate to filtered views

**Handled Validation:**
- No form validation (read-only view)
- Validates API response structure
- Handles null/empty data gracefully

**Types:**
- DTOs: `ListFeedsResponse`, `FeedListItemDto`, `ListFeedAnalysesResponse`, `FeedAnalysisListItemDto`, `PagingDto`
- ViewModels: `DashboardStatistics`, `DashboardData`

**Props:**
- None (root page component)

---

### StatisticsCardsSection

**Component Description:**
Container component that renders a grid of statistic cards displaying aggregated metrics. Uses MudBlazor's grid system to ensure responsive behavior (4 cards on desktop, 2 on tablet, 1 on mobile).

**Main Elements:**
- `<MudGrid>` with responsive column definitions
- Multiple `<MudItem>` wrappers for cards
- Each item renders a `StatisticCard` component

**Handled Interactions:**
- Click on card navigates to relevant filtered view (e.g., clicking "Failing Feeds" navigates to `/feeds?status=failed`)

**Handled Validation:**
- None (display-only)

**Types:**
- ViewModels: `DashboardStatistics`

**Props:**
- `Statistics` (DashboardStatistics): Aggregated metrics to display
- `OnCardClick` (EventCallback<string>): Fired when card is clicked with navigation target

---

### StatisticCard

**Component Description:**
Reusable card component displaying a single metric with icon, label, and value. Supports color coding based on metric severity and optional navigation on click.

**Main Elements:**
- `<MudCard>` with hover effect and optional click handler
- `<MudCardContent>` containing:
  - `<MudIcon>` for visual indicator
  - `<MudText Typo="Typo.h3">` for numeric value
  - `<MudText Typo="Typo.body2">` for label

**Handled Interactions:**
- Click event (if `OnClick` provided)
- Hover effect for clickable cards

**Handled Validation:**
- None

**Types:**
- Primitives: `string`, `int`

**Props:**
- `Icon` (string): MudBlazor icon name
- `Label` (string): Metric label (e.g., "Total Feeds")
- `Value` (int): Numeric value to display
- `Color` (Color): MudBlazor color for icon and accent
- `OnClick` (EventCallback): Optional click handler
- `IsClickable` (bool, default false): Whether card is interactive

---

### RecentActivitySection

**Component Description:**
Section displaying recent feed items across all user's feeds. Shows the latest 10-20 items with feed attribution, providing users with a quick overview of new content.

**Main Elements:**
- `<MudPaper>` section wrapper with padding
- `<MudText Typo="Typo.h5">` section heading
- `<RecentFeedItemsList>` child component
- Empty state message if no items exist

**Handled Interactions:**
- None directly (delegated to child components)

**Handled Validation:**
- None

**Types:**
- DTOs: `FeedItemDto[]`
- ViewModels: `RecentFeedItem`

**Props:**
- `RecentItems` (RecentFeedItem[]): Array of recent items with feed info

---

### RecentFeedItemsList

**Component Description:**
Scrollable list of recent feed items rendered as cards. Each item includes title, link, published date, and feed attribution. Displays items in chronological order (newest first).

**Main Elements:**
- `<MudList>` container with `Dense="true"`
- Multiple `<FeedItemCard>` components
- `<MudDivider>` between items
- "View All Feeds" link at bottom

**Handled Interactions:**
- Click on item navigates to source URL (opens in new tab)
- Click on feed attribution navigates to feed detail page

**Handled Validation:**
- None

**Types:**
- ViewModels: `RecentFeedItem`

**Props:**
- `Items` (RecentFeedItem[]): Recent items to display
- `MaxItems` (int, default 20): Maximum items to show

---

### FeedItemCard

**Component Description:**
Compact card displaying a single feed item with title, feed attribution, published date, and link. Optimized for dashboard view with minimal details.

**Main Elements:**
- `<MudCard Elevation="0">` (flat card)
- `<MudCardContent>`:
  - `<MudText Typo="Typo.h6">` for item title
  - `<MudText Typo="Typo.caption">` for feed name and date
  - `<MudIcon Icon="@Icons.Material.Filled.Launch">` for external link indicator

**Handled Interactions:**
- Click on title opens source URL in new tab
- Click on feed name navigates to feed detail

**Handled Validation:**
- None

**Types:**
- ViewModels: `RecentFeedItem`

**Props:**
- `Item` (RecentFeedItem): Feed item data
- `ShowFeedAttribution` (bool, default true): Whether to display feed name

---

### PendingAnalysesSection

**Component Description:**
Conditional section displayed only when user has pending feed analyses. Shows list of analyses awaiting completion or approval, with status indicators.

**Main Elements:**
- `<MudPaper>` section wrapper
- `<MudText Typo="Typo.h5">` section heading with badge showing count
- List of `<PendingAnalysisCard>` components
- "View All Analyses" link at bottom

**Handled Interactions:**
- Click on analysis card navigates to analysis detail
- Click "View All Analyses" navigates to `/feed-analyses`

**Handled Validation:**
- None

**Types:**
- DTOs: `FeedAnalysisListItemDto[]`

**Props:**
- `PendingAnalyses` (FeedAnalysisListItemDto[]): Analyses with status "pending" or "completed"
- `MaxItems` (int, default 5): Maximum analyses to show

---

### PendingAnalysisCard

**Component Description:**
Card displaying pending analysis with URL, status badge, warnings count, and timestamp. Provides quick access to analysis detail page.

**Main Elements:**
- `<MudCard>` with hover effect
- `<MudCardContent>`:
  - `<MudText Typo="Typo.subtitle1">` for target URL (truncated)
  - `<AnalysisStatusBadge>` component
  - `<MudChip>` for warnings count (if > 0)
  - `<MudText Typo="Typo.caption">` for timestamp

**Handled Interactions:**
- Click on card navigates to `/feed-analyses/{analysisId}`

**Handled Validation:**
- None

**Types:**
- DTOs: `FeedAnalysisListItemDto`

**Props:**
- `Analysis` (FeedAnalysisListItemDto): Analysis data
- `OnClick` (EventCallback): Click handler for navigation

---

### AnalysisStatusBadge

**Component Description:**
Reusable badge component displaying analysis status with color coding and icon. Supports statuses: Pending, Completed, Failed, Superseded.

**Main Elements:**
- `<MudChip>` with status-specific styling
- Status icon (spinner for pending, checkmark for completed, error for failed)
- Status text

**Handled Interactions:**
- Hover shows tooltip with status description

**Handled Validation:**
- None

**Types:**
- Enums: `FeedAnalysisStatus`

**Props:**
- `Status` (string): Analysis status
- `Size` (Size, default Size.Medium): Chip size
- `ShowIcon` (bool, default true): Whether to display icon

---

### QuickActionsSection

**Component Description:**
Section containing primary action buttons for common workflows. Provides easy access to feed creation and management.

**Main Elements:**
- `<MudPaper>` section wrapper
- `<MudGrid>` for button layout
- `<MudButton>` components with icons:
  - Create New Feed Analysis
  - View All Feeds
  - View All Analyses

**Handled Interactions:**
- Click on each button navigates to respective route

**Handled Validation:**
- None

**Types:**
- None (navigation only)

**Props:**
- None

---

### LoadingSkeleton

**Component Description:**
Reusable skeleton placeholder component displayed during data loading. Provides visual feedback while API requests are in progress.

**Main Elements:**
- `<MudGrid>` matching dashboard layout
- `<MudSkeleton>` components:
  - Rectangular skeletons for statistic cards
  - Text skeletons for section headings
  - Card skeletons for recent items

**Handled Interactions:**
- None (static placeholder)

**Handled Validation:**
- None

**Types:**
- None

**Props:**
- `Type` (string): Skeleton type ("statistics", "recentItems", "pendingAnalyses")

---

### ErrorDisplay

**Component Description:**
Shared component for displaying API errors in user-friendly format. Shows RFC 7807 problem details with retry option.

**Main Elements:**
- `<MudAlert Severity="Severity.Error">`
- Error title and message
- Optional technical details (correlation ID, status code)
- Retry button

**Handled Interactions:**
- Click retry button re-fetches data
- Click expand icon shows technical details

**Handled Validation:**
- None

**Types:**
- Custom: `ProblemDetails`

**Props:**
- `Error` (ProblemDetails?): Error information
- `OnRetry` (EventCallback): Retry action handler

## 5. Types

### DTO Types (from RSSVibe.Contracts)

**ListFeedsResponse**
```csharp
public sealed record ListFeedsResponse(
    FeedListItemDto[] Items,
    PagingDto Paging
);
```

**FeedListItemDto**
```csharp
public sealed record FeedListItemDto(
    Guid FeedId,
    Guid UserId,
    string SourceUrl,
    string NormalizedSourceUrl,
    string Title,
    string? Description,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    string? Etag,
    DateTime? LastModified,
    DateTime? LastParsedAt,
    DateTime? NextParseAfter,
    string? LastParseStatus,      // "succeeded", "failed", "pending", null
    int PendingParseCount,
    Guid? AnalysisId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RssUrl
);
```

**ListFeedAnalysesResponse**
```csharp
public sealed record ListFeedAnalysesResponse(
    FeedAnalysisListItemDto[] Items,
    PagingDto Paging
);
```

**FeedAnalysisListItemDto**
```csharp
public sealed record FeedAnalysisListItemDto(
    Guid AnalysisId,
    string TargetUrl,
    string Status,                 // "pending", "completed", "failed", "superseded"
    string[] Warnings,
    DateTimeOffset? AnalysisStartedAt,
    DateTimeOffset? AnalysisCompletedAt
);
```

**PagingDto**
```csharp
public sealed record PagingDto(
    int Skip,
    int Take,
    int TotalCount,
    bool HasMore
);
```

---

### ViewModel Types (New - to be created in RSSVibe.Client)

**DashboardData**
```csharp
public sealed record DashboardData(
    FeedListItemDto[] Feeds,
    FeedAnalysisListItemDto[] Analyses,
    RecentFeedItem[] RecentItems,
    DashboardStatistics Statistics
);
```
- **Purpose**: Aggregate container for all dashboard data fetched from APIs
- **Fields**:
  - `Feeds`: All user's feeds (used to calculate statistics)
  - `Analyses`: All user's feed analyses (filtered to pending/completed for display)
  - `RecentItems`: Recent feed items across all feeds
  - `Statistics`: Pre-calculated metrics

**DashboardStatistics**
```csharp
public sealed record DashboardStatistics(
    int TotalFeeds,
    int ActiveFeeds,
    int FailingFeeds,
    int PendingAnalyses
);
```
- **Purpose**: Aggregated metrics for statistic cards
- **Fields**:
  - `TotalFeeds`: Count of all feeds
  - `ActiveFeeds`: Count of feeds with `LastParseStatus == "succeeded"`
  - `FailingFeeds`: Count of feeds with `LastParseStatus == "failed"`
  - `PendingAnalyses`: Count of analyses with `Status == "pending"` or (`Status == "completed"` and no approved feed)

**RecentFeedItem**
```csharp
public sealed record RecentFeedItem(
    Guid ItemId,
    string Title,
    string Link,
    DateTimeOffset? PublishedAt,
    DateTimeOffset DiscoveredAt,
    string FeedTitle,
    Guid FeedId
);
```
- **Purpose**: Combined item and feed information for recent activity display
- **Fields**:
  - `ItemId`: Item identifier
  - `Title`: Article title
  - `Link`: Article URL
  - `PublishedAt`: Publication timestamp (nullable)
  - `DiscoveredAt`: When item was discovered by parser
  - `FeedTitle`: Name of parent feed
  - `FeedId`: Parent feed identifier for navigation

**Note**: The `RecentFeedItem` type requires data from both feeds and items, which necessitates either:
1. Multiple API calls (get feeds, then get items for each feed)
2. A dedicated dashboard endpoint (future enhancement)

For MVP, we'll use approach #1 with client-side aggregation.

## 6. State Management

**Pattern**: Component-level state with scoped services for shared concerns

**State Variables in Home.razor**:

```csharp
private DashboardData? _dashboardData;
private bool _isLoading = true;
private ProblemDetails? _error;
private CancellationTokenSource? _cts;
```

**State Lifecycle**:

1. **OnInitializedAsync**: Trigger data fetch
2. **LoadDashboardDataAsync**: Orchestrate API calls
3. **OnDispose**: Cancel pending requests

**No Custom Hook Required**: State is local to the Home component. No shared state management needed for MVP.

**Data Fetching Strategy**:

```csharp
protected override async Task OnInitializedAsync()
{
    _cts = new CancellationTokenSource();
    await LoadDashboardDataAsync();
}

private async Task LoadDashboardDataAsync()
{
    _isLoading = true;
    _error = null;

    try
    {
        // Fetch feeds with minimal pagination (we need all for statistics)
        var feedsResult = await ApiClient.Feeds.ListAsync(
            new ListFeedsRequest(
                Skip: 0,
                Take: 50,  // Should cover most users
                Sort: "lastParsedAt:desc",
                Status: null,
                Search: null
            ),
            _cts.Token
        );

        if (!feedsResult.IsSuccess || feedsResult.Data is null)
        {
            _error = new ErrorDisplay.ErrorInfo
            {
                Title = feedsResult.ErrorTitle ?? "Failed to load feeds",
                Detail = feedsResult.ErrorDetail ?? "Could not retrieve your feeds from the server."
            };
            return;
        }

        // Fetch analyses (only need pending/completed)
        var analysesResult = await ApiClient.FeedAnalyses.ListAsync(
            new ListFeedAnalysesRequest(
                Status: null,  // Get all, filter client-side
                Sort: "createdAt:desc",
                Skip: 0,
                Take: 20,
                Search: null
            ),
            _cts.Token
        );

        if (!analysesResult.IsSuccess || analysesResult.Data is null)
        {
            _error = new ErrorDisplay.ErrorInfo
            {
                Title = analysesResult.ErrorTitle ?? "Failed to load analyses",
                Detail = analysesResult.ErrorDetail ?? "Could not retrieve your feed analyses from the server."
            };
            return;
        }

        // Fetch recent items (aggregate from multiple feeds)
        var recentItems = await FetchRecentItemsAsync(feedsResult.Data.Items, _cts.Token);

        // Calculate statistics
        var statistics = CalculateStatistics(feedsResult.Data.Items, analysesResult.Data.Items);

        _dashboardData = new DashboardData(
            Feeds: feedsResult.Data.Items,
            Analyses: analysesResult.Data.Items,
            RecentItems: recentItems,
            Statistics: statistics
        );
    }
    catch (Exception ex)
    {
        _error = new ErrorDisplay.ErrorInfo
        {
            Title = "An unexpected error occurred",
            Detail = ex.Message
        };
    }
    finally
    {
        _isLoading = false;
        StateHasChanged();
    }
}
```

**FetchRecentItemsAsync Helper (Using Typed API Client)**:

```csharp
private async Task<IReadOnlyList<RecentFeedItem>> FetchRecentItemsAsync(
    IReadOnlyList<FeedListItemDto> feeds,
    CancellationToken cancellationToken)
{
    var recentItems = new List<RecentFeedItem>();

    // Limit to top 10 feeds by last parsed date to avoid too many API calls
    var feedsToFetch = feeds
        .Where(f => f.LastParsedAt.HasValue)
        .OrderByDescending(f => f.LastParsedAt)
        .Take(10)
        .ToList();

    foreach (var feed in feedsToFetch)
    {
        try
        {
            // ✅ CORRECT - Use typed API client
            var result = await ApiClient.Feeds.ListItemsAsync(
                feed.FeedId,
                new ListFeedItemsRequest(
                    Skip: 0,
                    Take: 5,
                    Sort: "publishedAt:desc",
                    Since: null,
                    Until: null,
                    ChangeKind: null,
                    IncludeMetadata: false
                ),
                cancellationToken);

            if (result.IsSuccess && result.Data?.Items is not null)
            {
                foreach (var item in result.Data.Items)
                {
                    recentItems.Add(new RecentFeedItem(
                        ItemId: item.ItemId,
                        Title: item.Title,
                        Link: item.Link,
                        PublishedAt: item.PublishedAt,
                        DiscoveredAt: item.DiscoveredAt,
                        FeedTitle: feed.Title,
                        FeedId: feed.FeedId
                    ));
                }
            }
        }
        catch
        {
            // Continue with next feed if this one fails
            continue;
        }
    }

    // Sort by published/discovered date, newest first, limit to 20
    return recentItems
        .OrderByDescending(i => i.PublishedAt ?? i.DiscoveredAt)
        .Take(20)
        .ToList();
}
```

**Client-Side Calculations**:

```csharp
private DashboardStatistics CalculateStatistics(
    FeedListItemDto[] feeds,
    FeedAnalysisListItemDto[] analyses)
{
    return new DashboardStatistics(
        TotalFeeds: feeds.Length,
        ActiveFeeds: feeds.Count(f => f.LastParseStatus == "succeeded"),
        FailingFeeds: feeds.Count(f => f.LastParseStatus == "failed"),
        PendingAnalyses: analyses.Count(a =>
            a.Status == "pending" ||
            (a.Status == "completed" && !feeds.Any(f => f.AnalysisId == a.AnalysisId))
        )
    );
}
```

## 7. API Integration

**Required API Calls**:

### 1. List Feeds
- **Endpoint**: `GET /api/v1/feeds`
- **API Client**: `ApiClient.Feeds.ListAsync()`
- **Request Type**: `ListFeedsRequest`
- **Request Parameters**:
  ```csharp
  new ListFeedsRequest(
      Skip: 0,
      Take: 50,
      Sort: "lastParsedAt:desc",
      Status: null,  // Get all
      Search: null
  )
  ```
- **Response Type**: `ApiResult<ListFeedsResponse>`
- **Purpose**: Fetch all user feeds for statistics and recent items

### 2. List Feed Analyses
- **Endpoint**: `GET /api/v1/feed-analyses`
- **API Client**: `ApiClient.FeedAnalyses.ListAsync()`
- **Request Type**: `ListFeedAnalysesRequest`
- **Request Parameters**:
  ```csharp
  new ListFeedAnalysesRequest(
      Status: null,  // Get all, filter client-side
      Sort: "createdAt:desc",
      Skip: 0,
      Take: 20,
      Search: null
  )
  ```
- **Response Type**: `ApiResult<ListFeedAnalysesResponse>`
- **Purpose**: Fetch analyses for pending analyses section

### 3. List Feed Items (per feed - aggregated)
- **Endpoint**: `GET /api/v1/feeds/{feedId}/items` (called for each feed)
- **API Client**: `ApiClient.Feeds.ListItemsAsync(feedId, request)`
- **Request Type**: `ListFeedItemsRequest`
- **Request Parameters**:
  ```csharp
  new ListFeedItemsRequest(
      Skip: 0,
      Take: 5,  // Only need recent items per feed
      Sort: "publishedAt:desc",
      Since: null,
      Until: null,
      ChangeKind: null,
      IncludeMetadata: false
  )
  ```
- **Response Type**: `ApiResult<ListFeedItemsResponse>`
- **Purpose**: Fetch recent items for activity feed
- **Optimization**: Limit to top 3-5 feeds by last parsed date to reduce API calls

**Error Handling**:
- Network errors: Display `ErrorDisplay` component with retry option
- 401 Unauthorized: Redirect to `/login` (handled by auth interceptor)
- 403 Forbidden: Display error message (shouldn't occur for user's own data)
- 5xx errors: Display generic error with retry option

**Caching Strategy**:
- No client-side caching for MVP
- Server-side caching via API response headers
- Future enhancement: Use localStorage for dashboard data with 5-minute TTL

## 8. User Interactions

### 1. Initial Page Load
**Trigger**: User navigates to `/`
**Action**:
- Display loading skeleton
- Fetch feeds, analyses, and items in parallel
- Calculate statistics client-side
- Render dashboard with data
- Hide loading skeleton

**Expected Outcome**: User sees populated dashboard with current metrics

---

### 2. Click on Statistic Card
**Trigger**: User clicks on "Failing Feeds" card
**Action**:
- Navigate to `/feeds?status=failed`
- Pre-apply filter to feeds list

**Expected Outcome**: User lands on feeds list showing only failing feeds

---

### 3. Click on Recent Feed Item
**Trigger**: User clicks on feed item title
**Action**:
- Open item source URL in new browser tab
- Use `target="_blank"` and `rel="noopener noreferrer"`

**Expected Outcome**: User can read article without leaving RSSVibe

---

### 4. Click on Feed Attribution
**Trigger**: User clicks on feed name in recent item
**Action**:
- Navigate to `/feeds/{feedId}`

**Expected Outcome**: User views detailed feed information

---

### 5. Click on Pending Analysis
**Trigger**: User clicks on pending analysis card
**Action**:
- Navigate to `/feed-analyses/{analysisId}`

**Expected Outcome**: User can review analysis details and create feed

---

### 6. Click Quick Action Button
**Trigger**: User clicks "Create New Feed Analysis"
**Action**:
- Navigate to `/feed-analyses` with create flow initiated (or modal)

**Expected Outcome**: User can start new feed creation process

---

### 7. Retry After Error
**Trigger**: User clicks "Retry" button in error display
**Action**:
- Clear error state
- Re-run `LoadDashboardDataAsync()`

**Expected Outcome**: Dashboard data reloads, potentially succeeding if error was transient

---

### 8. Empty State - No Feeds
**Trigger**: User has zero feeds and zero analyses
**Action**:
- Display empty state with prominent CTA
- "Get Started" button navigates to create analysis flow

**Expected Outcome**: User is guided to create their first feed

## 9. Conditions and Validation

### API Response Validation

**Feeds Response**:
- Validate `Items` is not null (fallback to empty array)
- Validate `Paging.TotalCount >= 0`
- Validate each `FeedListItemDto` has required fields:
  - `FeedId` is valid GUID
  - `Title` is not null/empty
  - `SourceUrl` is valid URL format

**Analyses Response**:
- Validate `Items` is not null
- Validate `Status` is known value ("pending", "completed", "failed", "superseded")
- Validate timestamps are in valid range

**Items Response**:
- Validate `Items` is not null per feed
- Validate `PublishedAt` is not future date (warn only, don't fail)

---

### Conditional Rendering

**Pending Analyses Section**:
- **Condition**: `PendingAnalyses.Length > 0`
- **Effect**: Section is only rendered when user has pending or completed analyses awaiting approval

**Empty States**:
- **No Feeds**: Display empty state with "Create Your First Feed" CTA
- **No Recent Items**: Display message "No items yet. Your feeds will update automatically."
- **No Pending Analyses**: Hide section entirely

**Error States**:
- **API Error**: Display `ErrorDisplay` component with retry button
- **Partial Failure**: If feeds load but items fail, show feeds with error notice for items section

---

### Component State Conditions

**Loading State**:
- **Condition**: `_isLoading == true`
- **Effect**: Display `LoadingSkeleton` instead of content

**Error State**:
- **Condition**: `_error != null`
- **Effect**: Display `ErrorDisplay` component

**Data Loaded State**:
- **Condition**: `_dashboardData != null && !_isLoading && _error == null`
- **Effect**: Render dashboard sections with data

---

### Statistics Card Color Coding

**Total Feeds**:
- **Color**: `Color.Primary`
- **Icon**: `Icons.Material.Filled.RssFeed`

**Active Feeds**:
- **Color**: `Color.Success`
- **Icon**: `Icons.Material.Filled.CheckCircle`

**Failing Feeds**:
- **Color**: `Color.Error` if count > 0, else `Color.Default`
- **Icon**: `Icons.Material.Filled.Error` if count > 0, else `Icons.Material.Filled.CheckCircle`
- **Clickable**: Only if count > 0

**Pending Analyses**:
- **Color**: `Color.Warning` if count > 0, else `Color.Default`
- **Icon**: `Icons.Material.Filled.Pending` if count > 0, else `Icons.Material.Filled.Done`
- **Clickable**: Only if count > 0

## 10. Error Handling

### Network Errors
**Scenario**: API request fails due to network connectivity
**Handling**:
- Catch exception in `LoadDashboardDataAsync`
- Set `_error` to `ProblemDetails` with user-friendly message
- Display `ErrorDisplay` component with retry button
- Log error to console for debugging

**User Guidance**: "Unable to connect to server. Please check your internet connection and try again."

---

### API Errors (4xx/5xx)
**Scenario**: API returns error response
**Handling**:
- Extract `ProblemDetails` from response
- Set `_error` state
- Display error title and detail
- Provide retry option

**User Guidance**: Display specific error message from API

---

### Partial Data Load Failure
**Scenario**: Feeds load successfully but items or analyses fail
**Handling**:
- Display feeds and statistics (partial success)
- Show warning alert for failed sections
- Allow user to retry failed sections individually

**User Guidance**: "Some data could not be loaded. Recent items are unavailable."

---

### Empty Data Set
**Scenario**: User has no feeds or analyses
**Handling**:
- Not an error condition
- Display friendly empty state with illustration
- Provide CTA to create first feed

**User Guidance**: "Welcome to RSSVibe! Get started by creating your first feed analysis."

---

### Authentication Expiration
**Scenario**: User's session expires while on dashboard
**Handling**:
- API client intercepts 401 response
- Redirect to `/login` with return URL
- Preserve dashboard route for post-login redirect

**User Guidance**: "Your session has expired. Please log in again."

---

### Timeout
**Scenario**: API request exceeds timeout threshold
**Handling**:
- Cancel request using `CancellationToken`
- Display timeout error
- Suggest retry or contact support

**User Guidance**: "Request timed out. The server may be experiencing high load. Please try again."

---

### Data Inconsistency
**Scenario**: Feed references analysis that doesn't exist in analyses list
**Handling**:
- Log warning to console
- Continue rendering without failing
- Display feed normally (analysis link may be broken)

**User Guidance**: No user-facing error (graceful degradation)

## 11. Implementation Steps

### Step 1: Create ViewModel Types
**Location**: `src/RSSVibe.Client/Models/Dashboard/`

**Files to create**:
1. `DashboardData.cs`
2. `DashboardStatistics.cs`
3. `RecentFeedItem.cs`

**Tasks**:
- Define record types matching specifications in Section 5
- Add XML documentation comments
- Ensure proper namespace (`RSSVibe.Client.Models.Dashboard`)

---

### Step 2: Create Shared Components
**Location**: `src/RSSVibe.Client/Components/Dashboard/`

**Files to create**:
1. `StatisticCard.razor` and `StatisticCard.razor.cs`
2. `FeedItemCard.razor` and `FeedItemCard.razor.cs`
3. `PendingAnalysisCard.razor` and `PendingAnalysisCard.razor.cs`
4. `AnalysisStatusBadge.razor` and `AnalysisStatusBadge.razor.cs`

**Tasks**:
- Implement components per Section 4 specifications
- Add proper parameter validation
- Include XML documentation
- Style with MudBlazor components
- Test in isolation (optional for MVP)

---

### Step 3: Create Section Components
**Location**: `src/RSSVibe.Client/Components/Dashboard/`

**Files to create**:
1. `StatisticsCardsSection.razor` and `StatisticsCardsSection.razor.cs`
2. `RecentActivitySection.razor` and `RecentActivitySection.razor.cs`
3. `RecentFeedItemsList.razor` and `RecentFeedItemsList.razor.cs`
4. `PendingAnalysesSection.razor` and `PendingAnalysesSection.razor.cs`
5. `QuickActionsSection.razor` and `QuickActionsSection.razor.cs`

**Tasks**:
- Implement section wrappers
- Use child components created in Step 2
- Apply responsive grid layouts
- Handle empty states

---

### Step 4: Create Loading and Error Components
**Location**: `src/RSSVibe.Client/Components/Shared/`

**Files to create**:
1. `LoadingSkeleton.razor` (if not already exists)
2. `ErrorDisplay.razor` (if not already exists)

**Tasks**:
- Implement skeleton placeholders matching dashboard layout
- Create error display with retry functionality
- Make components reusable across application

---

### Step 5: Implement Main Page Component
**Location**: `src/RSSVibe.Client/Pages/`

**File to create**:
`Home.razor` and `Home.razor.cs` (code-behind)

**Tasks**:
- Add `@page "/"` directive
- Wrap in `<AuthorizeView>`
- Implement state management (Section 6)
- Implement API integration (Section 7)
- Wire up all section components
- Handle loading, error, and empty states
- Add proper disposal of `CancellationTokenSource`

---

### Step 6: Implement Data Fetching Logic
**Location**: `Home.razor.cs`

**Tasks**:
- Implement `LoadDashboardDataAsync()` method
- Implement `FetchRecentItemsAsync()` helper
- Implement `CalculateStatistics()` helper
- Handle concurrent API calls safely
- Implement proper error handling and logging

---

### Step 7: Style and Responsive Design
**Location**: Component `.razor` files

**Tasks**:
- Apply MudBlazor grid system for responsive breakpoints
- Test on desktop (≥960px), tablet (600-959px), mobile (<600px)
- Ensure cards stack properly on smaller screens
- Verify statistic cards show 4/2/1 per row based on breakpoint
- Test touch interactions on mobile

---

### Step 8: Add Empty States
**Location**: `Home.razor`

**Tasks**:
- Create empty state for no feeds/analyses
- Add illustration or icon
- Provide clear CTA button
- Test empty state rendering

---

### Step 9: Implement Navigation Handlers
**Location**: Component code-behind files

**Tasks**:
- Wire up statistic card clicks to navigate with filters
- Implement feed attribution links
- Implement quick action button navigation
- Test all navigation paths

---

### Step 10: Testing and Refinement

**Manual Testing**:
- Test with empty data (new user)
- Test with 1-5 feeds
- Test with 20+ feeds
- Test with failing feeds
- Test with pending analyses
- Test error scenarios (network offline, API error)
- Test on multiple screen sizes
- Test with slow network (throttling)

**Accessibility Testing**:
- Verify semantic HTML structure
- Test keyboard navigation
- Test with screen reader
- Verify color contrast ratios
- Ensure focus indicators visible

**Performance Testing**:
- Measure initial load time
- Check for unnecessary re-renders
- Verify API calls are not duplicated
- Test with large data sets (50+ feeds)

---

### Step 11: Documentation

**Tasks**:
- Add code comments for complex logic
- Document component props in XML comments
- Update README if necessary
- Create component usage examples (optional)

---

### Step 12: Integration with Layout

**Location**: `src/RSSVibe.Client/Layout/MainLayout.razor`

**Tasks**:
- Ensure Home page renders within layout correctly
- Verify navigation drawer works
- Test breadcrumbs (should be hidden for home page)
- Verify app bar displays user info

---

### Step 13: Final Review and Polish

**Tasks**:
- Review all components against specification
- Check for consistent naming conventions
- Verify error messages are user-friendly
- Ensure loading states are smooth
- Test forced password change flow (if applicable)
- Verify authentication redirect works correctly
- Check for console errors/warnings

**Acceptance Criteria**:
- ✅ Dashboard displays statistics correctly
- ✅ Recent items load and display
- ✅ Pending analyses section shows when applicable
- ✅ Quick actions navigate to correct routes
- ✅ Error handling works for all scenarios
- ✅ Responsive design works on all breakpoints
- ✅ Loading states display correctly
- ✅ Empty states guide users appropriately
- ✅ All navigation paths function correctly
- ✅ No accessibility violations

---

## Implementation Checklist

- [ ] Create `DashboardData.cs` ViewModel
- [ ] Create `DashboardStatistics.cs` ViewModel
- [ ] Create `RecentFeedItem.cs` ViewModel
- [ ] Create `StatisticCard` component
- [ ] Create `FeedItemCard` component
- [ ] Create `PendingAnalysisCard` component
- [ ] Create `AnalysisStatusBadge` component
- [ ] Create `StatisticsCardsSection` component
- [ ] Create `RecentActivitySection` component
- [ ] Create `RecentFeedItemsList` component
- [ ] Create `PendingAnalysesSection` component
- [ ] Create `QuickActionsSection` component
- [ ] Create/update `LoadingSkeleton` component
- [ ] Create/update `ErrorDisplay` component
- [ ] Create `Home.razor` page component
- [ ] Implement data fetching logic
- [ ] Implement statistics calculation
- [ ] Wire up navigation handlers
- [ ] Add responsive design breakpoints
- [ ] Implement empty states
- [ ] Add error handling
- [ ] Test with various data scenarios
- [ ] Test on multiple devices/screen sizes
- [ ] Accessibility testing
- [ ] Performance testing
- [ ] Final review and polish
