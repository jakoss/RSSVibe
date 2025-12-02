# UI Architecture for RSSVibe

## 1. UI Structure Overview

RSSVibe's frontend is a Blazor WebAssembly Standalone application utilizing MudBlazor component library for a modern, responsive interface. The architecture emphasizes a clear separation between draft feed analyses and production-ready feeds, guiding users through a multi-step workflow from URL submission to RSS feed consumption.

### Core Architectural Principles

- **Progressive Disclosure**: Complex features are revealed progressively through wizard-style flows rather than overwhelming users with options upfront
- **Real-Time Feedback**: Server-Sent Events (SSE) provide live status updates for long-running operations (AI analysis, parsing)
- **Optimistic UI**: Immediate feedback for user actions with graceful rollback on failures
- **Authentication-First**: All features require authentication; dynamic navigation adapts to auth state
- **Accessibility**: WCAG AA compliant with semantic HTML, keyboard navigation, and screen reader support
- **Responsive Design**: Mobile-first approach using MudBlazor's breakpoint system

### Technical Foundation

- **Render Mode**: Interactive Auto (Blazor decides optimal rendering strategy)
- **Authentication**: Cookie-based with automatic server-side refresh token rotation
- **State Management**: Component-level state with scoped services for shared concerns (auth, notifications, SSE)
- **HTTP Integration**: Base HttpClient with service discovery and automatic credential inclusion
- **UI Preferences**: localStorage persistence for theme, drawer state, view modes, page sizes

### Information Architecture Hierarchy

```
Home (Dashboard)
├── Overview metrics
├── Recent activity feed
└── Quick actions

Authentication
├── Login
└── Change Password (forced flow for bootstrapped root user)

Feed Analyses
├── List View (analyses index with status filtering)
└── Detail View
    ├── Analysis status and warnings
    ├── Preflight check results
    ├── AI-detected selectors
    ├── Preview items (up to 10)
    ├── Raw HTML debugging
    └── Feed creation wizard

Feeds
├── List View (table/grid modes with filters and presets)
└── Detail View
    ├── Feed metadata
    ├── RSS URL with subscription links
    ├── Parse run history timeline
    ├── Feed items (paginated with filters)
    └── Configuration management
```

## 2. View List

### 2.1 Home Dashboard (`/`)

**Main Purpose**: Provide authenticated users with an at-a-glance overview of their feed ecosystem and quick access to common workflows.

**Key Information to Display**:
- Total feeds count (active, inactive, failing)
- Recent feed items across all feeds (latest 10-20 items)
- Parsing statistics (success rate, failed parses requiring attention)
- Pending feed analyses awaiting approval
- Quick action buttons (New Feed Analysis, View All Feeds, View All Analyses)
- System notifications (recent analysis completions, parse failures)

**Key View Components**:
- `StatisticsCards`: Aggregated metrics displayed as card grid
- `RecentActivityFeed`: Chronological list of recent items with feed attribution
- `QuickActionsPanel`: Primary action buttons for common workflows
- `NotificationsSummary`: Recent system events requiring user attention

**UX Considerations**:
- Load data from `GET /api/v1/feeds` and `GET /api/v1/feed-analyses` with minimal pagination
- Calculate statistics client-side from feed list responses
- Display skeleton loaders during data fetch
- Highlight feeds requiring attention (failed parses, warnings)
- Empty state prompts new users to create their first analysis

**Accessibility**:
- Semantic heading hierarchy (`<h1>` Dashboard, `<h2>` section titles)
- ARIA labels for statistics with numeric values announced
- Keyboard shortcuts hinted but not required for MVP
- Focus management on page load to main content

**Security**:
- Requires authentication via `<AuthorizeView>`
- Redirect to `/login` if unauthenticated
- Statistics aggregated from user's own feeds only

---

### 2.2 Login (`/login`)

**Main Purpose**: Authenticate users via email and password, handle forced password change redirection.

**Key Information to Display**:
- Email input field
- Password input field with visibility toggle
- "Remember Me" checkbox
- Login button
- Optional link to registration (if enabled via configuration)
- Validation errors from API (invalid credentials, account locked)

**Key View Components**:
- `LoginForm`: Email/password input with FluentValidation
- `ErrorDisplay`: RFC 7807 problem details component
- `LoadingSpinner`: Displayed during authentication request

**UX Considerations**:
- Client-side validation for email format and required fields
- Disable submit button during API request
- Display inline error messages below fields
- Automatic focus to email input on page load
- Remember Me persists auth state in localStorage
- Redirect to `/change-password` if `mustChangePassword: true` in response
- Otherwise redirect to home dashboard

**Accessibility**:
- Proper `<label>` associations with form inputs
- ARIA live region for error announcements
- Tab order follows visual flow
- Password visibility toggle announced to screen readers

**Security**:
- POST to `/api/v1/auth/login` with `useCookieAuth: true`
- Passwords never logged or persisted client-side
- HTTPS enforced for all authentication flows
- Rate limiting handled server-side (display 429 errors)

---

### 2.3 Change Password (`/change-password`)

**Main Purpose**: Force root user password rotation on first login; allow authenticated users to update passwords.

**Key Information to Display**:
- Current password input
- New password input with strength indicator
- Confirm new password input
- Submit button
- Password policy requirements (length ≥12, complexity)
- Success/error messages

**Key View Components**:
- `ChangePasswordForm`: Three input fields with validation
- `PasswordStrengthIndicator`: Visual feedback on new password quality
- `PasswordPolicyDisplay`: List of requirements with check/x indicators

**UX Considerations**:
- Display prominent warning if forced password change required
- Block access to all other routes via route guard until completed
- Validate new password strength client-side before submission
- Confirm password field validates match in real-time
- Success message with automatic redirect to home after 2 seconds
- Form reset on error for security

**Accessibility**:
- ARIA describedby links inputs to password policy
- Password strength announced via ARIA live region
- Error messages associated with specific fields
- Focus management returns to first invalid field on error

**Security**:
- POST to `/api/v1/auth/change-password`
- Current password verified server-side
- New password hashed server-side (PBKDF2)
- Clear `mustChangePassword` flag on success
- Refresh auth state after successful change

---

### 2.4 Feed Analyses List (`/feed-analyses`)

**Main Purpose**: Display all user's feed analyses with status, warnings, and quick actions for creating new analyses or viewing details.

**Key Information to Display**:
- Table or card grid of analyses (user preference persisted)
- Analysis ID (truncated for display)
- Target URL
- Status badge (pending, completed, failed, superseded)
- Warnings count with icon
- Created timestamp
- Completed timestamp (if applicable)
- Actions menu (View Details, Rerun, Delete draft)
- "Create New Feed Analysis" button prominently placed
- Pagination controls
- Filter/search bar (by URL, status)

**Key View Components**:
- `AnalysisListTable`: MudTable with sortable columns
- `AnalysisCardGrid`: Alternative card-based layout
- `AnalysisStatusBadge`: Color-coded status indicator
- `CreateAnalysisButton`: Primary action button
- `AnalysisSearchFilter`: Search and filter controls
- `SseStatusIndicator`: Live connection status for pending analyses

**UX Considerations**:
- Load from `GET /api/v1/feed-analyses` with pagination
- Default sort by `createdAt:desc`
- Status filter presets: All, Pending, Completed, Failed
- Search filters URL substring matches
- Empty state prompts user to create first analysis
- Pending analyses show pulsing status indicator
- SSE connection established for pending analyses to auto-refresh on completion
- View mode toggle (table/grid) persisted to localStorage

**Accessibility**:
- Table uses semantic `<thead>`, `<tbody>`, `<th>` elements
- Sort controls announced as buttons with current sort state
- Status badges include text label alongside color
- Card grid maintains tab order matching visual layout
- Filter controls labeled and grouped logically

**Security**:
- Requires authentication
- Only displays user's own analyses (enforced server-side)
- Delete action requires confirmation dialog

---

### 2.5 Feed Analysis Detail (`/feed-analyses/{analysisId}`)

**Main Purpose**: Display comprehensive analysis results including preflight checks, AI-detected selectors, warnings, and preview items. Provide inline selector editing and entry point to feed creation wizard.

**Key Information to Display**:
- Analysis status banner with real-time SSE updates
- Target URL and normalized URL
- Preflight check results with explanations:
  - RequiresJavascript warning
  - RequiresAuthentication warning
  - Paywalled warning
  - Each with color-coded severity and user guidance
- AI model used for analysis
- Detected selectors (list, item, title, link, published, summary)
- Raw HTML excerpt in expandable section
- Preview items (up to 10 extracted articles)
- Warning summary with counts
- Action buttons:
  - Edit Selectors (inline editing mode)
  - Test Selectors (fresh preview)
  - Create Feed (launches wizard)
  - Rerun Analysis
  - Delete Analysis

**Key View Components**:
- `AnalysisStatusBanner`: Real-time status with SSE updates
- `PreflightWarnings`: Alert-style warnings with icons and explanations
- `SelectorEditor`: Editable CSS selector inputs with syntax highlighting
- `PreviewItemCard`: Article preview card showing extracted fields
- `RawHtmlExcerpt`: Expandable code block with HTML excerpt
- `FeedCreationWizard`: Multi-step modal wizard for feed approval
- `SseConnectionManager`: Service managing real-time updates

**UX Considerations**:
- Load from `GET /api/v1/feed-analyses/{analysisId}`
- Establish SSE connection if status is pending
- Display loading skeleton for preview section until loaded
- Selector editing:
  - Inline edit mode toggles input fields
  - "Test Selectors" button triggers `PATCH /selectors` followed by `GET /preview?fresh=true`
  - Preview updates side-by-side with selector changes
  - Syntax highlighting for CSS selectors (basic: `.class`, `#id`, `element`, `[attr]`)
- Preview items displayed as cards with title, link, published date
- Raw HTML excerpt syntax-highlighted and limited to 500 characters
- Warnings prominently displayed with explanations:
  - RequiresJavascript: "This site may require JavaScript rendering. Extraction quality may be limited."
  - RequiresAuthentication: "This site requires login. Public feed cannot access authenticated content."
  - Paywalled: "This site has paywall restrictions. Feed may only capture preview content."
- Create Feed button disabled if blocking warnings exist (admin override not in MVP)
- Rerun analysis throttled to 15-minute cooldown (display countdown if active)

**Accessibility**:
- Status banner uses ARIA live region for SSE updates
- Preflight warnings use MudAlert with semantic alert role
- Selector inputs labeled with field purpose
- Preview items in ordered list with semantic structure
- Code blocks include language declaration for screen readers
- Modal wizard keyboard navigable with Esc to close

**Security**:
- Requires authentication
- Verify analysis belongs to user (403 if mismatch)
- PATCH operations validate selector schema server-side
- Rerun endpoint throttled server-side

---

### 2.6 Feed Creation Wizard (Modal/Stepper)

**Main Purpose**: Guide user through multi-step process of approving feed analysis and configuring feed metadata, schedule, and TTL.

**Steps**:

#### Step 1: Review Analysis Summary
- Display target URL
- Show detected selectors (read-only)
- Display warnings summary
- Preview item count
- Continue/Cancel buttons

#### Step 2: Acknowledge Warnings (if any)
- List all preflight warnings with detailed explanations
- Checkbox acknowledgment for each warning
- Emphasize potential quality limitations
- Continue disabled until all acknowledged

#### Step 3: Configure Feed Metadata
- Title input (required, max 200 chars)
- Description textarea (optional, max 2000 chars)
- Language dropdown (ISO 639 codes, default "en")
- Live character count for title and description

#### Step 4: Set Update Schedule
- Preset buttons: Hourly, Every 6 Hours, Daily, Weekly
- Custom interval inputs (unit dropdown, value number)
- Validation: minimum 1 hour interval
- Visual feedback for constraint violations
- Next parse time preview based on current time + interval

#### Step 5: Configure TTL
- TTL input (minutes, default 60, minimum 15)
- Explanation of TTL purpose for feed readers
- Preset buttons: 15, 30, 60, 120 minutes

#### Step 6: Review and Approve
- Summary of all configuration choices
- Feed RSS URL preview (`/feed/{userId}/{feedId}` format)
- Copy URL button
- Selector override option (show edited selectors if modified)
- Create Feed / Back buttons

**Key View Components**:
- `MudStepper` or custom wizard component
- `WizardStep` components for each step
- `ScheduleConfigurationInput`: Schedule picker with presets
- `TtlConfigurationInput`: TTL picker with presets
- `FeedConfigurationSummary`: Final review display

**UX Considerations**:
- Linear flow with back/next navigation
- Step validation before allowing next
- Progress indicator showing current step (e.g., "Step 3 of 6")
- Unsaved changes warning if user attempts to close
- Submit calls `POST /api/v1/feeds` with all configuration
- Success redirects to new feed detail page
- Error displays RFC 7807 problem details within wizard
- Optimistic UI: disable buttons during submission

**Accessibility**:
- Stepper uses ARIA step progression
- Each step has unique heading
- Form validation errors announced
- Focus management between steps
- Cancel and submit buttons clearly distinguished

**Security**:
- Validate analysisId ownership before submission
- Server-side validation of all inputs
- Schedule and TTL constraints enforced server-side

---

### 2.7 Feeds List (`/feeds`)

**Main Purpose**: Display all user's feeds with filtering, sorting, multiple view modes, and quick actions for management.

**Key Information to Display**:
- Table or card grid of feeds (user preference persisted)
- Feed ID (truncated)
- Title
- Source URL
- Language
- Last parsed timestamp
- Next parse timestamp
- Last parse status badge (succeeded, failed, pending)
- Update interval display (e.g., "Every 2 hours")
- Actions menu (View Details, Trigger Parse, Edit, Delete)
- "Create New Feed" button (launches Feed Analysis flow)
- Pagination controls
- Filter presets: Active Feeds, Parse Failures, Pending First Parse, Paused
- Sort options: Created, Last Parsed, Title
- Search bar (filter by title or URL)

**Key View Components**:
- `FeedListTable`: MudTable with sortable columns
- `FeedCardGrid`: Card-based layout alternative
- `FeedStatusBadge`: Color-coded parse status indicator
- `FeedFilterBar`: Presets and search input
- `ViewModeToggle`: Table/Grid toggle button
- `CreateFeedButton`: Primary action redirecting to analyses

**UX Considerations**:
- Load from `GET /api/v1/feeds` with pagination
- Default sort by `lastParsedAt:desc`
- Filter presets:
  - Active Feeds: `lastParseStatus=succeeded`
  - Parse Failures: `lastParseStatus=failed`
  - Pending First Parse: `lastParsedAt=null`
  - Paused: (client-side filter if pause feature exists, otherwise hidden)
- Search applies substring match on title and sourceUrl
- Empty state with prominent "Create Your First Feed" CTA
- View mode preference persisted to localStorage
- Trigger Parse action shows optimistic status update
- Delete requires confirmation with explanation of consequences
- SSE connection for feeds with active parse runs (optional enhancement)

**Accessibility**:
- Table headers sortable via button with announced state
- Card grid maintains logical reading order
- Status badges include text alongside color
- Filter presets keyboard navigable
- Actions menu uses semantic `<menu>` element

**Security**:
- Requires authentication
- Only displays user's own feeds
- Trigger Parse throttled server-side (429 if cooldown active)
- Delete cascades to items and parse runs (explained in confirmation)

---

### 2.8 Feed Detail (`/feeds/{feedId}`)

**Main Purpose**: Comprehensive view of individual feed including metadata, RSS URL, parse history, configuration, and paginated feed items.

**Key Information to Display**:

**Feed Metadata Section**:
- Feed title
- Description
- Source URL (clickable link)
- Language
- Update interval
- TTL setting
- Created timestamp
- Last parsed timestamp
- Next parse timestamp
- Last parse status
- Edit Feed button

**RSS URL Section**:
- Full RSS URL display
- Copy to Clipboard button
- Quick subscription links:
  - Feedly: `https://feedly.com/i/subscription/feed/{encodedUrl}`
  - Inoreader: `https://www.inoreader.com/feed/{encodedUrl}`
  - NewsBlur: `https://newsblur.com/?url={encodedUrl}`
- QR code generation (excluded from MVP per session notes)

**Parse History Section**:
- Visual timeline of recent parse runs (last 20)
- Color-coded success/failure indicators
- Clickable timeline points for run details
- Trigger Manual Parse button
- View Full History link to paginated parse runs

**Feed Items Section**:
- Paginated table/list of items
- Filters:
  - Date range picker (since/until)
  - Change kind (new, refreshed, unchanged)
- Sort: Published Date (default desc), Discovered Date, Last Seen Date
- Item display fields:
  - Title (clickable link to source)
  - Published date
  - Discovered date
  - Change kind badge
  - Expand for rawMetadata (author, tags, word count)
- Page size selector (25, 50, 100)
- Pagination controls with "Showing X-Y of Z items"

**Key View Components**:
- `FeedMetadataCard`: Feed configuration display
- `RssUrlDisplay`: RSS URL with copy and subscription links
- `FeedStatusTimeline`: Visual parse run history
- `FeedItemsList`: Paginated items table with filters
- `FeedItemDetailExpansion`: Expandable rawMetadata display
- `EditFeedModal`: Configuration editing form
- `ParseRunDetailModal`: Individual run details

**UX Considerations**:
- Load feed from `GET /api/v1/feeds/{feedId}`
- Load items from `GET /api/v1/feeds/{feedId}/items` with pagination
- Load parse runs from `GET /api/v1/feeds/{feedId}/parse-runs` for timeline
- RSS URL copy shows success snackbar notification
- Subscription links open in new tab
- Manual parse trigger:
  - Optimistic UI: immediately show "Parsing..." status
  - Disable trigger button for cooldown period
  - SSE connection for real-time parse progress (optional enhancement)
  - Rollback if API fails with 409 or 429
- Timeline visualization:
  - Horizontal timeline with dots for each run
  - Green = succeeded, Red = failed, Yellow = pending
  - Hover tooltip shows run details
  - Click opens full run detail modal
- Items list:
  - Default includes title, link, dates, change kind
  - `includeMetadata=true` when user expands item
  - Empty state if no items parsed yet with "Trigger Parse" CTA
- Edit feed opens modal with current configuration
- Delete feed from this view requires confirmation

**Accessibility**:
- Timeline uses semantic list with ARIA labels
- Copy button announces success to screen readers
- Items table uses proper table semantics
- Expandable metadata uses disclosure widget pattern
- Modal dialogs trap focus and announce title

**Security**:
- Requires authentication
- Verify feed ownership (403 if mismatch)
- Manual parse respects rate limits
- Edit operations validate ownership

---

### 2.9 Parse Run Detail (Modal or Dedicated View)

**Main Purpose**: Display detailed metrics and diagnostics for individual parse run, including items discovered in that run.

**Key Information to Display**:
- Parse run ID
- Feed title and ID
- Started timestamp
- Completed timestamp
- Duration (calculated client-side)
- Status (succeeded, failed, pending)
- Failure reason (if failed)
- HTTP status code
- Response headers (ETag, Last-Modified) if `includeResponseHeaders=true`
- Fetched items count
- New items count
- Updated items count
- Skipped items count
- Retry count (resiliency metric)
- Circuit breaker status (resiliency metric)
- Items discovered in this run (paginated, with change kind filter)

**Key View Components**:
- `ParseRunMetricsCard`: Summary statistics
- `ParseRunResiliencyMetrics`: Retry and circuit breaker info
- `ParseRunItemsList`: Items table filtered to this run
- `ParseRunErrorDisplay`: Failure reason with sanitized error message

**UX Considerations**:
- Load from `GET /api/v1/feeds/{feedId}/parse-runs/{runId}`
- Load items from `GET /api/v1/feeds/{feedId}/parse-runs/{runId}/items`
- Display duration in human-readable format (e.g., "8.3 seconds")
- Failure reason sanitized for user display (no stack traces)
- Items list shows only items from this specific run with change kind
- Color coding:
  - Green for succeeded
  - Red for failed
  - Yellow for pending
- Retry count > 0 indicates transient failures (show info tooltip)
- Circuit breaker open indicates systemic issues (show warning)

**Accessibility**:
- Metrics displayed in definition list (`<dl>`)
- Status uses semantic color and text
- Error messages in alert region
- Items table follows standard table semantics

**Security**:
- Requires authentication
- Verify feed ownership before displaying run

---

### 2.10 Public RSS Feed (Non-authenticated)

**Path**: `/feed/{userId}/{feedId}`

**Main Purpose**: Serve standard RSS 2.0 XML for anonymous consumption by feed readers.

**Key Information to Display**:
- RSS 2.0 XML structure
- Channel metadata:
  - Title
  - Description
  - Link (source URL)
  - Language
  - LastBuildDate
  - TTL
  - Atom self-link
- Items (sorted by publishedAt desc):
  - Title
  - Link
  - PubDate
  - Description/Summary (if available)
  - GUID (item ID)

**UX Considerations**:
- This is not a browser-rendered view; consumed by feed readers
- Browser display shows raw XML with syntax highlighting
- HTTP caching headers (ETag, Last-Modified, Cache-Control)
- Conditional requests (304 Not Modified) for efficiency
- Rate limiting enforced server-side

**Accessibility**:
- Not applicable (consumed by machines)

**Security**:
- No authentication required (public URL)
- Feed must be active (not deleted)
- IP-based rate limiting (1000 req/hour per session notes)
- HTTPS enforced
- No exposure of rawMetadata or user information

---

## 3. User Journey Map

### Journey 1: Creating an RSS Feed (Primary Use Case)

**Goal**: User wants to convert a website into an RSS feed.

**Steps**:

1. **Entry Point**: User lands on Home Dashboard after authentication
   - View: `/` (Home Dashboard)
   - User sees "Create New Feed Analysis" button or empty state CTA

2. **Navigate to Feed Analyses**: User clicks "Create New Feed Analysis"
   - View: `/feed-analyses`
   - User sees list of existing analyses (if any) and creation form

3. **Submit Website URL**: User enters target URL in prominent input field
   - Action: Client-side validation checks HTTPS format
   - Submit triggers `POST /api/v1/feed-analyses`
   - View: Redirects to `/feed-analyses/{analysisId}` immediately

4. **Monitor Analysis Progress**: System performs AI analysis
   - View: `/feed-analyses/{analysisId}`
   - SSE connection established for real-time status updates
   - User sees "Analyzing..." status with spinner
   - Status updates: pending → analyzing → completed/failed
   - Duration: 10-60 seconds typically

5. **Review Analysis Results**: Analysis completes successfully
   - View: Updated `/feed-analyses/{analysisId}` with completed status
   - User sees:
     - Preflight warnings (if any) with explanations
     - AI-detected selectors (list, item, title, link, published)
     - Preview of up to 10 extracted items
     - Raw HTML excerpt for debugging

6. **Optional Selector Editing**: User notices incorrect item extraction
   - Action: User clicks "Edit Selectors"
   - Inline editing mode activates for selector fields
   - User modifies `.title` selector
   - User clicks "Test Selectors"
   - Action: `PATCH /api/v1/feed-analyses/{analysisId}/selectors` then `GET /preview?fresh=true`
   - Preview updates showing corrected extraction
   - User satisfied with results

7. **Initiate Feed Creation**: User clicks "Create Feed" button
   - View: Feed Creation Wizard modal opens over analysis detail

8. **Wizard Step 1 - Review Summary**: User confirms analysis selections
   - Modal: Review Analysis Summary step
   - User clicks "Continue"

9. **Wizard Step 2 - Acknowledge Warnings**: If warnings exist
   - Modal: Acknowledge Warnings step
   - User reads warning explanations
   - User checks acknowledgment boxes
   - User clicks "Continue"

10. **Wizard Step 3 - Configure Metadata**: User enters feed information
    - Modal: Configure Feed Metadata step
    - User enters title: "Example Tech News"
    - User enters description: "Latest technology articles from Example.com"
    - User selects language: "en"
    - User clicks "Continue"

11. **Wizard Step 4 - Set Schedule**: User configures update frequency
    - Modal: Set Update Schedule step
    - User clicks "Every 6 Hours" preset
    - Next parse time preview updates
    - User clicks "Continue"

12. **Wizard Step 5 - Configure TTL**: User sets cache behavior
    - Modal: Configure TTL step
    - User accepts default 60 minutes
    - User clicks "Continue"

13. **Wizard Step 6 - Review and Approve**: User confirms all settings
    - Modal: Review and Approve step
    - User sees summary of all choices
    - RSS URL preview displayed
    - User clicks "Create Feed"
    - Action: `POST /api/v1/feeds` with full configuration
    - Modal shows loading spinner

14. **Feed Created Successfully**: System creates feed
    - Modal: Success message displays
    - After 2 seconds, auto-redirect to `/feeds/{feedId}`

15. **View Feed Details**: User lands on new feed detail page
    - View: `/feeds/{feedId}`
    - User sees:
      - Feed metadata
      - RSS URL with copy button
      - Quick subscription links
      - Parse history (empty, awaiting first parse)
      - Feed items (empty)
    - User copies RSS URL
    - Success snackbar: "URL copied to clipboard"

16. **Subscribe in Feed Reader**: User uses RSS URL
    - User pastes URL into Feedly/other reader
    - Reader fetches feed using `GET /feed/{userId}/{feedId}`
    - Reader displays feed content

17. **Monitor Feed Updates**: User returns to check feed status
    - View: `/feeds/{feedId}` or `/feeds` list
    - User sees successful parse runs in timeline
    - Feed items populate automatically per schedule

**Pain Points Addressed**:
- **Technical complexity**: Wizard simplifies feed creation with clear steps and presets
- **Uncertainty about quality**: Preview shows extraction results before commitment
- **Configuration errors**: Validation prevents invalid schedules and TTL values
- **Lack of feedback**: Real-time SSE updates eliminate polling and uncertainty
- **Discovery of RSS URL**: Prominent display with copy button and subscription links
- **Debugging extraction**: Raw HTML excerpt and selector editing enable troubleshooting

---

### Journey 2: Monitoring Feed Performance (Secondary Use Case)

**Goal**: User notices feeds aren't updating or want to check health.

**Steps**:

1. **Entry Point**: User logs in and views Home Dashboard
   - View: `/` (Home Dashboard)
   - User sees "Parse Failures" badge on statistics card
   - User clicks badge or navigates to Feeds

2. **Apply Filter Preset**: User identifies problematic feeds
   - View: `/feeds` with "Parse Failures" preset selected
   - List shows only feeds with `lastParseStatus=failed`
   - User sees "Example News Feed" with red status badge

3. **View Feed Details**: User clicks feed title
   - View: `/feeds/{feedId}`
   - User sees:
     - Last parse: 2 hours ago
     - Last status: Failed
     - Parse history timeline showing recent failures (red dots)

4. **Inspect Failed Parse Run**: User clicks recent failed run in timeline
   - View: Parse Run Detail modal opens
   - User sees:
     - Status: Failed
     - Failure reason: "HTTP 503 Service Unavailable"
     - Retry count: 3
     - Circuit breaker: Open
   - User understands source website is temporarily down

5. **Check Feed Items**: User scrolls to items section
   - View: Feed items list on same page
   - User sees items from previous successful parses
   - User confirms feed was working before outage

6. **Decision: Wait or Investigate**: User decides to wait for auto-recovery
   - User notes next parse time in feed metadata
   - User closes modal
   - Circuit breaker will auto-close after cooldown

7. **Alternative: Edit Selectors**: If issue is extraction quality
   - User clicks "Edit Feed" button
   - Modal opens with editable configuration
   - User updates selectors
   - Action: `PATCH /api/v1/feeds/{feedId}` with new selectors
   - User clicks "Trigger Manual Parse"
   - Optimistic UI: Status shows "Parsing..."
   - SSE updates provide real-time progress

8. **Verify Fix**: User monitors parse completion
   - View: `/feeds/{feedId}` with SSE connection
   - Parse completes successfully
   - Timeline updates with new green dot
   - Items list refreshes showing newly extracted articles
   - User confirms extraction quality improved

9. **Return to Dashboard**: User navigates to home
   - View: `/` (Home Dashboard)
   - Statistics updated showing reduced failure count
   - User satisfied with resolution

**Pain Points Addressed**:
- **Lack of visibility**: Dashboard highlights issues immediately
- **Diagnostic difficulty**: Parse run details explain failure reasons
- **No control over parsing**: Manual trigger allows on-demand parsing
- **Uncertainty about fix**: Real-time feedback confirms selector changes work
- **Waiting for updates**: Timeline visualization shows recovery patterns

---

### Journey 3: Forced Password Change (Security Flow)

**Goal**: Bootstrapped root user must change password on first login.

**Steps**:

1. **Initial Login Attempt**: Root user logs in with default password
   - View: `/login`
   - User enters root credentials
   - Action: `POST /api/v1/auth/login`
   - Response includes `mustChangePassword: true`

2. **Route Guard Intercepts**: System detects forced password flag
   - View: Immediate redirect to `/change-password`
   - Prominent warning banner explains requirement
   - User cannot access any other routes

3. **Read Password Policy**: User reviews requirements
   - View: `/change-password` with policy display
   - User sees:
     - Minimum 12 characters
     - Complexity requirements
     - Current password field
     - New password field
     - Confirm password field

4. **Enter Passwords**: User fills form
   - User enters current password
   - User enters new password
   - Strength indicator shows "Strong" with green color
   - User confirms new password
   - Match indicator shows checkmark

5. **Submit Password Change**: User clicks "Change Password"
   - Action: `POST /api/v1/auth/change-password`
   - Button disables during request
   - Loading spinner displays

6. **Password Changed Successfully**: System updates credentials
   - View: Success message displays
   - Auth state refreshed via `GET /api/v1/auth/profile`
   - `mustChangePassword` flag cleared
   - After 2 seconds, auto-redirect to `/` (Home)

7. **Access Granted**: User lands on dashboard
   - View: `/` (Home Dashboard)
   - User has full access to all features
   - Route guard no longer blocks navigation

**Pain Points Addressed**:
- **Security vulnerability**: Forces credential rotation for default accounts
- **Confusion**: Clear explanation of requirement with banner
- **Password strength**: Real-time feedback prevents weak passwords
- **Navigation escape**: Route guard prevents bypassing requirement

---

## 4. Layout and Navigation Structure

### Overall Layout Pattern

RSSVibe uses a standard application shell layout with responsive behavior:

```
┌─────────────────────────────────────────────────────────┐
│  App Bar (MudAppBar)                                    │
│  ┌──────────┐ ┌───────────────────┐ ┌────────┐         │
│  │ Hamburger│ │ RSSVibe Logo      │ │ Profile│         │
│  └──────────┘ └───────────────────┘ └────────┘         │
├──────────────────┬──────────────────────────────────────┤
│                  │                                      │
│  Navigation      │  Main Content Area                   │
│  Drawer          │  (MudMainContent)                    │
│  (MudDrawer)     │                                      │
│                  │                                      │
│  • Home          │  [Current page content with          │
│  • Feed Analyses │   breadcrumbs at top]                │
│  • Feeds         │                                      │
│                  │                                      │
│  [Bottom items]  │                                      │
│  • Dark Mode     │                                      │
│  • Logout        │                                      │
│                  │                                      │
└──────────────────┴──────────────────────────────────────┘
```

### MudBlazor Layout Components

**MudLayout**: Root layout container
- Wraps entire application
- Manages drawer and app bar positioning
- Handles responsive breakpoints

**MudAppBar**: Top application bar
- Fixed position
- Left: Hamburger menu icon (toggles drawer)
- Center: RSSVibe logo (clickable, navigates to `/`)
- Right:
  - Notification bell icon with badge (unread count)
  - Dark mode toggle icon button
  - User profile menu (avatar/initials, dropdown with Logout)

**MudDrawer**: Side navigation panel
- Persistent on desktop (≥960px)
- Overlay on mobile (<960px)
- Toggleable via hamburger or gesture
- State persisted to localStorage (`drawerOpen` key)
- Contains navigation menu items

**MudMainContent**: Primary content area
- Scrollable region
- Contains breadcrumbs component at top
- Contains page-specific content below
- Responsive padding based on breakpoint

### Navigation Menu Structure

**Authenticated Users**:
```
┌─────────────────────────┐
│ RSSVibe                 │
├─────────────────────────┤
│ 🏠 Home                 │
│ 🔍 Feed Analyses        │
│ 📡 Feeds                │
├─────────────────────────┤
│ 🌙 Dark Mode [Toggle]   │
│ 🚪 Logout               │
└─────────────────────────┘
```

**Unauthenticated Users**:
```
┌─────────────────────────┐
│ RSSVibe                 │
├─────────────────────────┤
│ 🔑 Login                │
└─────────────────────────┘
```

### Dynamic Navigation with AuthorizeView

Navigation items adapt based on authentication state using Blazor's `<AuthorizeView>` component:

```razor
<AuthorizeView>
    <Authorized>
        <MudNavLink Href="/" Icon="@Icons.Material.Filled.Home">Home</MudNavLink>
        <MudNavLink Href="/feed-analyses" Icon="@Icons.Material.Filled.Analytics">Feed Analyses</MudNavLink>
        <MudNavLink Href="/feeds" Icon="@Icons.Material.Filled.RssFeed">Feeds</MudNavLink>
    </Authorized>
    <NotAuthorized>
        <MudNavLink Href="/login" Icon="@Icons.Material.Filled.Login">Login</MudNavLink>
    </NotAuthorized>
</AuthorizeView>
```

### Breadcrumb Navigation

Breadcrumbs display hierarchical location below app bar within main content:

**Examples**:
- Home: No breadcrumbs
- Feed Analyses List: `Home → Feed Analyses`
- Analysis Detail: `Home → Feed Analyses → example.com`
- Feeds List: `Home → Feeds`
- Feed Detail: `Home → Feeds → Example Tech News`
- Parse Run Detail: `Home → Feeds → Example Tech News → Parse Run #123`

**Implementation**:
- MudBreadcrumbs component
- Dynamic generation based on route parameters
- Feed/analysis titles fetched from current page data
- Clickable breadcrumb items navigate to parent routes

### Notification Center

**App Bar Integration**:
- Bell icon in top right
- Badge shows unread notification count
- Click opens notification dropdown panel

**Notification Panel**:
- MudPopover anchored to bell icon
- List of recent notifications (last 10)
- Notification types:
  - Analysis completed
  - Parse failed
  - Manual parse completed
  - Feed creation successful
- Each notification:
  - Icon indicating type
  - Message text
  - Timestamp (relative, e.g., "5 minutes ago")
  - Clickable link to relevant resource
- "View All" link at bottom (future enhancement)
- "Clear All" button

**Notification Service**:
- Scoped service managing notification state
- Receives events from SSE connections
- Persists to localStorage for session continuity
- Maximum 50 notifications stored (FIFO queue)

### Profile Menu

**App Bar Integration**:
- User avatar or initials in circle
- Hover shows user email tooltip
- Click opens profile dropdown

**Dropdown Items**:
- Email display (read-only)
- Display name (read-only)
- Separator
- "Change Password" link (navigates to `/change-password`)
- "Logout" button (calls logout action)

### Responsive Behavior

**Desktop (≥960px - MudBreakpoint.Md+)**:
- Drawer persistent and open by default
- App bar spans full width
- Main content adjusts margin for drawer
- Tables display all columns
- Card grids show 3-4 cards per row

**Tablet (600-959px - MudBreakpoint.Sm)**:
- Drawer overlay mode, closed by default
- Hamburger menu always visible
- Main content full width when drawer closed
- Tables hide less important columns
- Card grids show 2 cards per row

**Mobile (<600px - MudBreakpoint.Xs)**:
- Drawer overlay mode, closed by default
- Compact app bar with minimal items
- Main content full width
- Tables switch to card-like mobile layout
- Card grids show 1 card per row
- Forms stack vertically

### Routing and Navigation Guards

**Route Configuration**:
- `/` - Home Dashboard (requires auth)
- `/login` - Login (public)
- `/change-password` - Change Password (requires auth)
- `/feed-analyses` - Analyses List (requires auth)
- `/feed-analyses/{analysisId}` - Analysis Detail (requires auth)
- `/feeds` - Feeds List (requires auth)
- `/feeds/{feedId}` - Feed Detail (requires auth)
- `/feed/{userId}/{feedId}` - Public RSS Feed (public, XML response)

**Route Guards**:
1. **Authentication Guard**:
   - Applied to all routes except `/login` and `/feed/*`
   - Redirects unauthenticated users to `/login`
   - Implemented via `<AuthorizeView>` with `<RedirectToLogin />`

2. **Forced Password Change Guard**:
   - Intercepts navigation on all routes
   - If `mustChangePassword: true` in auth state
   - Redirects to `/change-password`
   - Only allows `/change-password` and `/login` routes
   - Clears on successful password change

3. **Ownership Verification**:
   - Feed and analysis detail pages verify resource ownership
   - `403 Forbidden` displays error page if mismatch
   - User redirected to list view with error message

### Deep Linking and Refresh Behavior

**Auth State Restoration**:
- On app initialization, check localStorage for cached auth state
- If present, validate via `GET /api/v1/auth/profile`
- If invalid, clear cache and redirect to `/login`
- If valid, restore user context and proceed to requested route

**Deep Link Handling**:
- User can bookmark or share specific feed/analysis URLs
- Auth guard intercepts, redirects to `/login` if needed
- After successful login, redirect to originally requested URL
- Return URL stored in navigation state

**Browser Refresh**:
- Auth state restored from localStorage
- Page-specific data refetched from API
- SSE connections reestablished if applicable
- User sees brief loading state, then normal page

---

## 5. Key Components

### 5.1 Authentication & Security Components

#### `RedirectToLogin`
**Purpose**: Handle unauthenticated access attempts by redirecting to login page.

**Usage**: Nested within `<AuthorizeView><NotAuthorized>` blocks on protected routes.

**Features**:
- Captures current route as return URL
- Displays brief "Redirecting..." message
- Navigates to `/login` with state

---

#### `ChangePasswordForm`
**Purpose**: Capture and validate password change requests with security requirements.

**Props**:
- `IsForcedChange` (bool): Whether this is mandatory password rotation

**Features**:
- Current password input (type=password with toggle)
- New password input with strength indicator
- Confirm password input with match validation
- Password policy checklist with visual feedback
- Real-time validation feedback
- Submit button disabled until valid

**Validation**:
- Current password required
- New password ≥12 characters
- New password meets complexity requirements
- Confirm password matches new password
- New password differs from current password

**Events**:
- `OnPasswordChanged` (EventCallback): Fired on successful change

---

#### `CustomAuthStateProvider`
**Purpose**: Manage authentication state and token lifecycle.

**Responsibilities**:
- Load cached auth state from localStorage on initialization
- Validate cached state via `GET /api/v1/auth/profile`
- Update state on login/logout
- Notify components of auth state changes via `AuthenticationStateChanged` event
- Clear state and cache on logout

**Properties**:
- `IsAuthenticated` (bool)
- `CurrentUser` (User model with email, displayName, roles, mustChangePassword)
- `AccessToken` (string, for API requests)

---

#### `ClientAccessTokenProvider` (implements `IAccessTokenProvider`)
**Purpose**: Provide access tokens to HttpClient for API authentication.

**Responsibilities**:
- Retrieve current access token from `CustomAuthStateProvider`
- Automatic injection into HttpClient Authorization header
- Cookie-based auth flow coordination

---

### 5.2 Error Handling & Notification Components

#### `ErrorDisplay`
**Purpose**: Render RFC 7807 problem details in user-friendly format.

**Props**:
- `ProblemDetails` (object): Problem details from API response
- `ShowTechnicalDetails` (bool, default false): Toggle for correlation ID, trace

**Features**:
- Primary error message display (title and detail)
- Validation errors list (if applicable)
- Error code badge
- Expandable technical details section:
  - Correlation ID with copy button
  - HTTP status code
  - Timestamp
- Color-coded severity (error, warning, info)

**Styling**:
- MudAlert component with appropriate severity
- Icon based on error type
- Accessible ARIA labels

---

#### `NotificationCenter`
**Purpose**: Display persistent notification history with actions.

**Features**:
- Popover panel anchored to app bar bell icon
- Scrollable notification list
- Notification item structure:
  - Icon (analysis complete, parse failed, etc.)
  - Message text
  - Relative timestamp
  - Click to navigate to relevant resource
  - Dismiss button
- Unread indicator (bold text, background highlight)
- "Mark All Read" button
- "Clear All" button
- Empty state message

**Data Source**:
- `NotificationService` scoped service
- Receives events from SSE connections and explicit API actions
- Persists to localStorage

---

#### `NotificationService`
**Purpose**: Centralized service for managing notifications across the application.

**Methods**:
- `AddNotification(type, message, targetUrl)`: Add new notification
- `MarkAsRead(notificationId)`: Mark single notification read
- `MarkAllAsRead()`: Mark all notifications read
- `ClearAll()`: Remove all notifications
- `GetUnreadCount()`: Return count for badge

**Events**:
- `OnNotificationsChanged` (EventCallback): Notifies subscribers of state changes

**Storage**:
- localStorage key: `rssvibeNotifications`
- JSON serialized array of notification objects
- Maximum 50 items (FIFO)

---

### 5.3 Feed Analysis Components

#### `AnalysisStatusBadge`
**Purpose**: Display analysis status with color coding and icon.

**Props**:
- `Status` (enum: Pending, Completed, Failed, Superseded)

**Features**:
- MudChip component with status-specific styling
- Status text (e.g., "Completed")
- Icon indicating status (spinner, checkmark, error, replaced)
- Color coding:
  - Pending: Yellow/amber with spinner
  - Completed: Green with checkmark
  - Failed: Red with error icon
  - Superseded: Gray with archive icon
- Tooltip with status description on hover

---

#### `PreflightWarnings`
**Purpose**: Display preflight check warnings with explanations and severity.

**Props**:
- `Warnings` (array of strings): Warning codes (RequiresJavascript, etc.)
- `Details` (object): Additional preflight details

**Features**:
- MudAlert for each warning with appropriate severity
- Warning icon
- Warning title (human-readable)
- Explanation text:
  - RequiresJavascript: "This site may require JavaScript rendering. Extraction quality may be limited."
  - RequiresAuthentication: "This site requires login. Public feed cannot access authenticated content."
  - Paywalled: "This site has paywall restrictions. Feed may only capture preview content."
- Severity color coding (warning/error)
- Collapsible detail section if `Details` provided

---

#### `SelectorEditor`
**Purpose**: Editable CSS selector inputs with syntax highlighting and testing.

**Props**:
- `Selectors` (FeedSelectors object): Current selector values
- `AnalysisId` (guid): Analysis ID for testing
- `ReadOnly` (bool, default false): Display-only mode

**Features**:
- Input field for each selector type:
  - List selector
  - Item selector
  - Title selector
  - Link selector
  - Published selector (optional)
  - Summary selector (optional)
- Basic syntax highlighting for CSS (classes, IDs, elements, attributes)
- "Test Selectors" button (if not ReadOnly)
- Loading indicator during test
- Validation feedback (invalid selector syntax)

**Events**:
- `OnSelectorsChanged` (EventCallback<FeedSelectors>): Fires on successful test
- `OnValidationError` (EventCallback<string>): Fires on validation failure

**Actions**:
- Test Selectors: `PATCH /api/v1/feed-analyses/{analysisId}/selectors` then `GET /preview?fresh=true`

---

#### `PreviewItemCard`
**Purpose**: Display extracted article preview with formatted fields.

**Props**:
- `Item` (PreviewItem object): Contains title, link, publishedAt, rawHtmlExcerpt

**Features**:
- Card layout with:
  - Article title (bold, large)
  - Link (clickable, truncated, with domain display)
  - Published date (formatted, e.g., "May 1, 2024 at 10:00 AM")
  - Expandable raw HTML excerpt (syntax highlighted)
- Hover effect on card
- Clickable entire card area opens link in new tab

---

#### `RawHtmlExcerpt`
**Purpose**: Display raw HTML code excerpt with syntax highlighting.

**Props**:
- `HtmlContent` (string): Raw HTML excerpt (max 500 chars)

**Features**:
- MudExpansionPanel for collapsible display
- Code block with HTML syntax highlighting
- Copy button for excerpt
- Monospace font
- Scrollable if exceeds panel height

---

#### `FeedCreationWizard`
**Purpose**: Multi-step modal wizard for approving analysis and creating feed.

**Props**:
- `AnalysisId` (guid): Analysis to approve
- `Analysis` (FeedAnalysis object): Full analysis data

**Features**:
- MudDialog modal container
- Stepper navigation (6 steps)
- Step components:
  1. `ReviewAnalysisSummary`
  2. `AcknowledgeWarnings` (conditional)
  3. `ConfigureFeedMetadata`
  4. `SetUpdateSchedule`
  5. `ConfigureTtl`
  6. `ReviewAndApprove`
- Back/Next/Cancel navigation buttons
- Step validation before advancing
- Loading state during submission
- Error display within wizard

**Events**:
- `OnFeedCreated` (EventCallback<Guid>): Fires with new feedId on success
- `OnCancelled` (EventCallback): Fires on user cancellation

**Actions**:
- Submit: `POST /api/v1/feeds` with collected configuration

---

### 5.4 Feed Management Components

#### `FeedCard`
**Purpose**: Card-based feed display for grid view mode.

**Props**:
- `Feed` (Feed object): Feed metadata

**Features**:
- Card with:
  - Feed title (heading)
  - Source URL (truncated, with tooltip)
  - Last parse status badge
  - Last parsed timestamp (relative)
  - Next parse timestamp (relative)
  - Update interval display
  - Quick actions menu (View, Trigger Parse, Edit, Delete)
- Status color accent on card border
- Hover effect
- Clickable card navigates to feed detail

---

#### `FeedStatusTimeline`
**Purpose**: Visual timeline of parse run history with success/failure indicators.

**Props**:
- `FeedId` (guid): Feed identifier
- `Limit` (int, default 20): Number of runs to display

**Features**:
- Horizontal timeline with dots for each parse run
- Color-coded dots:
  - Green: succeeded
  - Red: failed
  - Yellow: pending
- Hover tooltip shows:
  - Run ID
  - Started timestamp
  - Status
  - Duration
  - Item counts
- Click dot opens parse run detail modal
- Responsive: stacks vertically on mobile

**Data Source**:
- `GET /api/v1/feeds/{feedId}/parse-runs?take={limit}`

---

#### `FeedItemsList`
**Purpose**: Paginated, filterable list of feed items with expandable details.

**Props**:
- `FeedId` (guid): Feed identifier
- `DefaultPageSize` (int, default 25): Initial page size

**Features**:
- Table layout with columns:
  - Title (link to source)
  - Published date
  - Discovered date
  - Change kind badge
  - Expand icon for metadata
- Filter controls:
  - Date range picker (since/until)
  - Change kind dropdown (all, new, refreshed, unchanged)
- Sort dropdown (publishedAt desc, discoveredAt desc, lastSeenAt desc)
- Page size selector (25, 50, 100)
- Pagination controls with "Showing X-Y of Z items"
- Expandable row shows rawMetadata:
  - Author
  - Tags (as chips)
  - Word count
- Empty state with CTA to trigger parse

**Data Source**:
- `GET /api/v1/feeds/{feedId}/items` with query parameters

---

#### `RssUrlDisplay`
**Purpose**: Display RSS feed URL with copy functionality and subscription links.

**Props**:
- `RssUrl` (string): Full RSS URL
- `FeedTitle` (string): Feed title for encoding

**Features**:
- Read-only text input with full URL
- "Copy to Clipboard" button with success feedback
- Quick subscription links:
  - Feedly
  - Inoreader
  - NewsBlur
- Each link opens in new tab with pre-filled subscription form
- URL encoding handled automatically

**Actions**:
- Copy: Uses Clipboard API, shows success snackbar
- Subscription links: Generate reader-specific URLs with encoded RSS URL

---

#### `ScheduleConfigurationInput`
**Purpose**: User-friendly schedule picker with presets and validation.

**Props**:
- `CurrentSchedule` (UpdateInterval object): Existing schedule (for edit)
- `OnScheduleChanged` (EventCallback<UpdateInterval>): Fires on valid change

**Features**:
- Preset buttons:
  - Hourly (1 hour)
  - Every 6 Hours
  - Daily (24 hours)
  - Weekly (168 hours)
- Custom option:
  - Unit dropdown (hour, day, week)
  - Value number input (min 1)
- Validation:
  - Minimum 1-hour total interval
  - Visual error message if constraint violated
  - Next parse time preview based on selection
- Highlight selected preset

---

#### `EditFeedModal`
**Purpose**: Modal dialog for editing feed configuration.

**Props**:
- `FeedId` (guid): Feed to edit
- `CurrentFeed` (Feed object): Existing feed data

**Features**:
- MudDialog modal
- Form fields:
  - Title (editable)
  - Description (editable)
  - Language (editable)
  - Selectors override (SelectorEditor component)
  - Schedule (ScheduleConfigurationInput component)
  - TTL (number input with presets)
- Save/Cancel buttons
- Validation feedback
- Loading state during save

**Events**:
- `OnSaved` (EventCallback): Fires on successful update
- `OnCancelled` (EventCallback): Fires on user cancellation

**Actions**:
- Save: `PATCH /api/v1/feeds/{feedId}` with updated fields

---

### 5.5 Parse Run Components

#### `ParseRunMetricsCard`
**Purpose**: Display summary metrics for parse run in card format.

**Props**:
- `ParseRun` (FeedParseRun object): Parse run data

**Features**:
- Card with metrics:
  - Duration (calculated from start/completed timestamps)
  - Status badge
  - HTTP status code
  - Fetched items count
  - New items count
  - Updated items count
  - Skipped items count
- Color-coded status indicator
- Responsive layout (stacks on mobile)

---

#### `ParseRunResiliencyMetrics`
**Purpose**: Display resiliency metrics (retries, circuit breaker) for parse run.

**Props**:
- `ParseRun` (FeedParseRun object): Parse run data

**Features**:
- Expandable section (collapsed by default)
- Metrics:
  - Retry count with interpretation tooltip
  - Circuit breaker status with explanation
  - HTTP status code
  - Response headers (ETag, Last-Modified)
- Warning display if retry count > 0 or circuit breaker open

---

#### `ParseRunItemsList`
**Purpose**: Display items discovered in specific parse run with change kind filter.

**Props**:
- `FeedId` (guid): Feed identifier
- `RunId` (guid): Parse run identifier

**Features**:
- Table layout with columns:
  - Title
  - Link
  - Change kind badge
  - Seen at timestamp
- Filter by change kind (all, new, refreshed, unchanged)
- Pagination controls
- Empty state if no items in run

**Data Source**:
- `GET /api/v1/feeds/{feedId}/parse-runs/{runId}/items`

---

#### `ParseRunErrorDisplay`
**Purpose**: Display sanitized error information for failed parse runs.

**Props**:
- `FailureReason` (string): Sanitized error message

**Features**:
- MudAlert with error severity
- Error message text
- Collapsible technical details (if available)
- Suggested actions based on error type:
  - HTTP 503: "Source website temporarily unavailable. Auto-retry scheduled."
  - HTTP 404: "Source page not found. Verify URL is correct."
  - Timeout: "Request timed out. Source may be slow or unresponsive."

---

### 5.6 Common UI Components

#### `BreadcrumbsProvider`
**Purpose**: Generate and display breadcrumb navigation based on current route.

**Features**:
- MudBreadcrumbs component
- Dynamic breadcrumb generation from route
- Route parameter inspection for IDs
- Title fetching from API for dynamic labels
- Clickable breadcrumb items navigate to parent routes
- Responsive: truncate on mobile

---

#### `SseConnectionService`
**Purpose**: Manage Server-Sent Events connections for real-time updates.

**Methods**:
- `ConnectToAnalysis(analysisId)`: Subscribe to analysis status updates
- `ConnectToParseRun(feedId)`: Subscribe to parse run progress updates
- `Disconnect(connectionId)`: Close specific connection
- `DisconnectAll()`: Close all connections

**Events**:
- `OnAnalysisStatusChanged` (EventCallback<AnalysisStatusUpdate>): Fires on analysis update
- `OnParseRunProgressChanged` (EventCallback<ParseRunProgressUpdate>): Fires on parse update

**Responsibilities**:
- Manage EventSource connections
- Handle connection errors and reconnection
- Parse SSE event data
- Notify subscribers via events
- Clean up connections on component disposal

**Note**: SSE endpoint specifications unresolved in session notes; implementation depends on API team's SSE design.

---

#### `LocalStoragePreferences`
**Purpose**: Manage user preferences in browser localStorage.

**Methods**:
- `GetTheme()`: Return 'dark' or 'light'
- `SetTheme(theme)`: Store theme preference
- `GetDrawerState()`: Return true/false for drawer open/closed
- `SetDrawerState(isOpen)`: Store drawer state
- `GetViewMode(context)`: Return 'table' or 'grid' for specific view (feeds, analyses)
- `SetViewMode(context, mode)`: Store view mode preference
- `GetPageSize(context)`: Return page size for specific view
- `SetPageSize(context, size)`: Store page size preference

**Storage Keys**:
- `rssvibeTheme`: Theme preference
- `rssvibeDrawerOpen`: Drawer state
- `rssvibeViewMode_{context}`: View mode per context
- `rssvibePageSize_{context}`: Page size per context

---

#### `ConfirmationDialog`
**Purpose**: Reusable confirmation dialog for destructive actions.

**Props**:
- `Title` (string): Dialog title
- `Message` (string): Confirmation message
- `ConfirmText` (string, default "Confirm"): Confirm button text
- `CancelText` (string, default "Cancel"): Cancel button text
- `Severity` (enum, default Warning): Color scheme (info, warning, error)

**Features**:
- MudDialog modal
- Icon based on severity
- Title and message display
- Confirm/Cancel buttons with appropriate colors
- Keyboard support (Enter confirms, Esc cancels)

**Events**:
- `OnConfirmed` (EventCallback): Fires on confirmation
- `OnCancelled` (EventCallback): Fires on cancellation

---

#### `LoadingSkeleton`
**Purpose**: Display loading placeholders while data fetches.

**Props**:
- `Type` (enum): Card, Table, List
- `Count` (int, default 3): Number of skeleton items

**Features**:
- Animated skeleton shapes matching content type
- Responsive sizing
- MudSkeleton components from MudBlazor

---

#### `EmptyState`
**Purpose**: Display friendly empty state messages with CTAs.

**Props**:
- `Icon` (string): MudBlazor icon name
- `Title` (string): Empty state title
- `Message` (string): Descriptive message
- `ActionText` (string, optional): CTA button text
- `OnAction` (EventCallback, optional): CTA action

**Features**:
- Centered layout
- Large icon
- Title and message text
- Optional CTA button
- Used for:
  - No feeds created yet
  - No items in feed
  - No analyses performed
  - No search results

---

### 5.7 User Interface Patterns Summary

**Consistency Patterns**:
- All list views support table and grid modes
- All paginated views use consistent controls and page size options
- All forms use MudBlazor input components with validation
- All modals use MudDialog with consistent styling
- All status indicators use color + text + icon for accessibility
- All timestamps display relative format with absolute tooltip
- All loading states use skeleton or spinner consistently
- All errors use ErrorDisplay component for RFC 7807 responses
- All destructive actions require confirmation dialog

**Accessibility Patterns**:
- All interactive elements keyboard navigable
- All forms use proper label associations
- All status changes announced via ARIA live regions
- All color-coded elements include text/icon alternatives
- All modals trap focus and announce title
- All tables use semantic HTML structure
- All errors provide context and actionable guidance

**Responsive Patterns**:
- Tables collapse to cards on mobile breakpoints
- Navigation drawer switches to overlay on mobile
- Form layouts stack vertically on mobile
- Card grids adjust column count based on screen size
- Font sizes scale based on breakpoint
- Touch targets sized appropriately for mobile (≥44px)

---

## 6. Mapping Requirements to UI Elements

### PRD User Stories → UI Implementation

#### US-001: User Registration and Secure Login
**PRD Requirements**:
- User can create an account using ASP.NET Identity
- Login must validate against securely stored credentials
- Appropriate error messages for invalid credentials

**UI Implementation**:
- **View**: Login (`/login`)
- **Components**: `LoginForm`, `ErrorDisplay`
- **Features**:
  - Email and password inputs with validation
  - "Remember Me" checkbox
  - Error display for 401/400 responses
  - Cookie-based authentication
  - Redirect to dashboard on success
- **Status**: ✅ Fully covered

---

#### US-002: Submit Website for Feed Generation
**PRD Requirements**:
- Platform accepts valid URL input
- Pre-flight analysis triggered
- User informed if URL unsuitable

**UI Implementation**:
- **Views**: Feed Analyses List (`/feed-analyses`), Analysis Detail (`/feed-analyses/{analysisId}`)
- **Components**: `CreateAnalysisButton`, `AnalysisStatusBadge`, `PreflightWarnings`
- **Features**:
  - URL input form with HTTPS validation
  - Automatic redirect to analysis detail after submission
  - SSE connection for real-time status updates
  - Preflight warnings displayed prominently with explanations
- **Status**: ✅ Fully covered

---

#### US-003: AI-Powered Feed Analysis
**PRD Requirements**:
- AI analysis returns selectable strategy with selectors
- Errors/warnings clearly presented
- Preview of up to 10 extracted items shown

**UI Implementation**:
- **View**: Analysis Detail (`/feed-analyses/{analysisId}`)
- **Components**: `SelectorEditor`, `PreviewItemCard`, `PreflightWarnings`, `RawHtmlExcerpt`
- **Features**:
  - Display all detected selectors (list, item, title, link, published)
  - Show warnings with color coding and explanations
  - Preview section with up to 10 item cards
  - Raw HTML excerpt for debugging
- **Status**: ✅ Fully covered

---

#### US-004: Edit and Approve AI Suggestions
**PRD Requirements**:
- User can modify suggested selectors
- Real-time preview updates with modified selectors
- On approval, system saves configuration and creates feed

**UI Implementation**:
- **View**: Analysis Detail with inline editing
- **Components**: `SelectorEditor`, `FeedCreationWizard`
- **Features**:
  - Inline selector editing with syntax highlighting
  - "Test Selectors" button for fresh preview
  - Side-by-side preview update
  - Multi-step wizard for feed approval
  - POST to create feed on final approval
- **Status**: ✅ Fully covered

---

#### US-005: Configure Feed Update Schedule
**PRD Requirements**:
- User selects between hourly, daily, weekly intervals
- System enforces minimum 1-hour frequency
- Configuration stored and triggers scheduled parsing

**UI Implementation**:
- **Component**: `ScheduleConfigurationInput` (within `FeedCreationWizard` and `EditFeedModal`)
- **Features**:
  - Preset buttons: Hourly, Every 6 Hours, Daily, Weekly
  - Custom interval with unit and value inputs
  - Validation: minimum 1-hour constraint with visual feedback
  - Next parse time preview
- **Status**: ✅ Fully covered

---

#### US-006: Public Feed Access
**PRD Requirements**:
- RSS feed publicly accessible at `/feed/{userId}/{feed_guid}`
- Feed conforms to RSS 2.0 specifications
- Feed updates automatically based on schedule

**UI Implementation**:
- **View**: Public RSS Feed (non-UI, XML response)
- **Component**: `RssUrlDisplay` (shows URL to users)
- **Features**:
  - Feed detail page displays RSS URL prominently
  - Copy to clipboard button
  - Quick subscription links for popular feed readers
  - Public endpoint serves RSS 2.0 XML
  - HTTP caching headers for efficient delivery
- **Status**: ✅ Fully covered

---

### Functional Requirements → UI Coverage

#### 1. User Authentication and Setup
**Requirement**: Secure login with forced password change for root user

**UI Coverage**:
- Login view with cookie-based authentication
- Change Password view with forced flow
- Route guard blocking access until password changed
- Password policy display and validation
- **Status**: ✅ Complete

---

#### 2. Feed Creation Flow
**Requirement**: URL submission → preflight → AI analysis → selector review → approval

**UI Coverage**:
- Feed Analyses list with creation form
- Analysis detail with real-time status updates
- Preflight warnings display
- Selector editing with preview
- Multi-step wizard for approval
- **Status**: ✅ Complete

---

#### 3. Parsing and Scheduling
**Requirement**: Background parsing with user-defined frequencies, caching, resiliency

**UI Coverage**:
- Schedule configuration in feed wizard and edit modal
- Manual parse trigger on feed detail
- Parse history timeline visualization
- Parse run detail with metrics and resiliency info
- Real-time progress updates via SSE (pending API design)
- **Status**: ⚠️ Mostly complete (SSE implementation pending)

---

#### 4. Extraction and Storage
**Requirement**: Extract key fields, generate UUIDs, maintain deduplication

**UI Coverage**:
- Feed items list showing extracted fields
- Item detail with rawMetadata
- Change kind badges (new, refreshed, unchanged)
- Parse run items showing deduplication results
- **Status**: ✅ Complete

---

#### 5. RSS Generation
**Requirement**: Generate RSS 2.0 feeds with required metadata, public access

**UI Coverage**:
- RSS URL display on feed detail
- Copy and subscription link functionality
- Public endpoint serves XML (non-UI)
- **Status**: ✅ Complete

---

#### 6. Telemetry and Resilience
**Requirement**: OpenTelemetry integration, logging, resiliency metrics

**UI Coverage**:
- Parse run detail shows retry counts and circuit breaker status
- Failure reasons displayed with context
- No explicit telemetry UI (operational concern)
- **Status**: ✅ Complete (user-facing aspects)

---

### Product Boundaries Verification

#### Included in MVP ✅
- User authentication and secure login: **Login, Change Password views**
- Website management (add, view, modify, delete): **Feeds list, feed detail, edit modal**
- AI-powered analysis with preview: **Analysis detail, selector editor, preview items**
- Automated RSS generation: **RSS URL display, public endpoint**
- Configurable update schedules: **Schedule configuration component**
- Standard RSS delivery: **Public RSS endpoint integration**

#### Excluded from MVP ✅
- Full article content extraction: **Not implemented**
- Visual highlighting or advanced editing UI: **Basic selector editing only**
- Team collaboration: **Not implemented**
- Custom feed URLs: **Standard URL format only**
- Per-article fetching: **Not implemented**
- JavaScript-rendered site support: **Flagged in warnings, not supported**
- Storage caps and pruning: **Not implemented**
- User profile page: **Confirmed not required per session notes**
- QR codes: **Confirmed not required per session notes**
- Keyboard shortcuts: **Confirmed not required per session notes**
- Inline item actions: **Confirmed not required per session notes**

---

## 7. Edge Cases and Error States

### Authentication Edge Cases

#### Expired Session
**Scenario**: User's access token expires during active session.

**Handling**:
- Cookie-based auth with automatic server-side refresh
- If refresh fails, `CustomAuthStateProvider` detects 401 responses
- User redirected to `/login` with return URL preserved
- Error message: "Your session has expired. Please log in again."

#### Account Locked
**Scenario**: User exceeds failed login attempts.

**Handling**:
- API returns 423 Locked status
- `ErrorDisplay` shows: "Your account has been locked due to multiple failed login attempts. Please contact support."
- Login form disabled
- No retry option (server-side lockout duration)

#### Password Change Required While Navigating
**Scenario**: User receives `mustChangePassword: true` mid-session (admin forces reset).

**Handling**:
- Route guard intercepts next navigation attempt
- Redirect to `/change-password` with message
- All routes blocked except password change
- User must complete password rotation

---

### Analysis Creation Edge Cases

#### Duplicate URL Submission
**Scenario**: User submits URL already analyzed.

**Handling**:
- API returns 409 Conflict
- `ErrorDisplay` shows: "You have already analyzed this URL. View existing analysis instead."
- Display link to existing analysis detail page

#### OpenRouter API Unavailable
**Scenario**: AI provider is down or unreachable.

**Handling**:
- API returns 503 Service Unavailable
- Analysis status shows "Failed" with reason: "AI analysis service temporarily unavailable."
- "Rerun Analysis" button enabled after cooldown
- User can retry later

#### Invalid URL Format
**Scenario**: User enters non-HTTPS or malformed URL.

**Handling**:
- Client-side validation prevents submission
- Inline error message: "URL must be a valid HTTPS address (e.g., https://example.com)"
- Submit button disabled until corrected

#### Analysis Timeout
**Scenario**: AI analysis takes longer than expected.

**Handling**:
- SSE connection maintains status updates
- If no update after 5 minutes, display message: "Analysis is taking longer than usual. Please wait or check back later."
- User can navigate away and return
- Status will update when complete

---

### Selector Editing Edge Cases

#### Invalid CSS Selector Syntax
**Scenario**: User enters malformed selector (e.g., `.class[`).

**Handling**:
- Client-side validation detects syntax error
- Inline error message: "Invalid CSS selector syntax. Please correct and try again."
- "Test Selectors" button disabled
- Provide example of correct syntax

#### Selectors Return Zero Items
**Scenario**: Modified selectors don't match any elements.

**Handling**:
- Preview API returns empty items array
- Display warning: "No items extracted with current selectors. Please review and adjust."
- Show raw HTML excerpt for debugging
- "Create Feed" button disabled until items extracted

#### Preview Fetch Fails
**Scenario**: Target website is down during preview request.

**Handling**:
- API returns 503 or timeout
- `ErrorDisplay` shows: "Unable to fetch preview. Source website may be temporarily unavailable."
- Preview section shows error state with retry button
- Selectors can still be edited and saved for later

---

### Feed Creation Edge Cases

#### Feed Creation with Blocking Warnings
**Scenario**: User attempts to create feed with RequiresAuthentication warning.

**Handling**:
- "Create Feed" button disabled with tooltip: "This site requires authentication and cannot generate a public feed."
- User informed in warning panel
- No admin override in MVP

#### Feed Creation Failure Mid-Wizard
**Scenario**: API error occurs during final submit.

**Handling**:
- Wizard remains open
- `ErrorDisplay` shows error details within wizard
- User can correct issues (if validation error) or retry
- Wizard state preserved (no data loss)

#### Duplicate Source URL
**Scenario**: User attempts to create feed for URL already assigned to another feed.

**Handling**:
- API returns 409 Conflict
- `ErrorDisplay` shows: "You already have a feed for this URL. View existing feed instead."
- Wizard shows link to existing feed detail page

---

### Feed Management Edge Cases

#### Feed Deletion While Parse Running
**Scenario**: User attempts to delete feed with active parse job.

**Handling**:
- API returns 409 Conflict
- `ErrorDisplay` shows: "Cannot delete feed while parsing is in progress. Please wait for current parse to complete."
- Delete button disabled with tooltip during parse

#### Manual Parse Trigger During Cooldown
**Scenario**: User clicks "Trigger Parse" within rate limit window.

**Handling**:
- API returns 429 Too Many Requests
- `ErrorDisplay` shows: "Manual parse rate limit exceeded. Please wait X minutes before retrying."
- Display countdown timer until cooldown expires
- Button disabled until cooldown complete

#### Circuit Breaker Open
**Scenario**: Feed has repeated failures, circuit breaker opens.

**Handling**:
- Parse status badge shows "Circuit Open" with warning color
- Tooltip explains: "Parsing temporarily suspended due to repeated failures. Auto-retry scheduled."
- Display circuit breaker status in parse run detail
- User cannot trigger manual parse until circuit closes

#### Feed Update Conflict
**Scenario**: User edits feed while background parser simultaneously updates metadata.

**Handling**:
- Optimistic update applies user changes immediately
- If API returns 409 or 412 Precondition Failed, rollback changes
- `ErrorDisplay` shows: "Feed was updated by another process. Please reload and try again."
- Refresh button to reload current state

---

### Pagination Edge Cases

#### Last Page After Deletion
**Scenario**: User deletes last item on page, leaving page empty.

**Handling**:
- Automatically navigate to previous page
- If first page becomes empty, show empty state
- No pagination controls displayed if single page

#### Out-of-Bounds Offset
**Scenario**: User manually edits URL with invalid offset (e.g., `skip=9999`).

**Handling**:
- API returns empty items array with correct total count
- Display: "No items found on this page. Showing page 1 instead."
- Reset pagination to first page

#### Page Size Change Resets Offset
**Scenario**: User changes page size while on page 5.

**Handling**:
- Reset to first page with new page size
- Preserve other filters and sort order
- Display message: "Page size changed. Showing page 1 of results."

---

### SSE Connection Edge Cases

#### SSE Connection Failure
**Scenario**: Browser cannot establish SSE connection (network issue, server issue).

**Handling**:
- Fall back to polling every 5 seconds (degraded mode)
- Display status indicator: "Live updates unavailable. Using periodic refresh."
- Automatic reconnection attempts every 30 seconds

#### SSE Connection Timeout
**Scenario**: SSE connection idle for extended period.

**Handling**:
- Server sends periodic keep-alive messages
- If no message received for 60 seconds, assume connection lost
- Attempt reconnection
- Display reconnecting status

#### Multiple Tabs Open
**Scenario**: User opens multiple tabs with same analysis or feed.

**Handling**:
- Each tab maintains independent SSE connection
- Server handles multiple connections per user
- All tabs receive updates simultaneously
- No conflicting state (read-only SSE data)

---

### Network and API Error States

#### 500 Internal Server Error
**Scenario**: Unexpected server error during API request.

**Handling**:
- `ErrorDisplay` shows: "An unexpected error occurred. Please try again later."
- Display correlation ID with copy button: "Error ID: {correlationId}"
- Guidance: "If problem persists, contact support with Error ID."

#### Network Timeout
**Scenario**: API request exceeds timeout threshold.

**Handling**:
- Display error: "Request timed out. Please check your connection and try again."
- Retry button available
- Optimistic UI changes rolled back

#### Offline Mode
**Scenario**: User loses internet connection.

**Handling**:
- Display banner: "You appear to be offline. Some features may not work."
- Queue actions for retry when connection restored (optional enhancement)
- Cached data remains visible
- Disable action buttons requiring network

---

### Mobile-Specific Edge Cases

#### Small Screen Table Display
**Scenario**: Tables too wide for mobile viewport.

**Handling**:
- Switch to card-based layout on mobile breakpoints
- Each table row becomes a card with stacked fields
- Preserve sorting and filtering functionality

#### Touch Gesture Conflicts
**Scenario**: Swipe gestures conflict with carousel or drawer.

**Handling**:
- Clearly defined touch target areas
- Drawer swipe only active on edge of screen
- Scrollable areas don't interfere with swipe

#### Keyboard Visibility Issues
**Scenario**: Mobile keyboard covers form inputs.

**Handling**:
- Auto-scroll to focused input when keyboard appears
- Fixed position elements adjust for keyboard
- Submit button remains accessible above keyboard

---

### Browser Compatibility Edge Cases

#### Clipboard API Unavailable
**Scenario**: Older browser without Clipboard API support.

**Handling**:
- Fall back to `document.execCommand('copy')`
- If that fails, show message: "Copy not supported. Please manually select and copy."

#### localStorage Full
**Scenario**: localStorage quota exceeded (rare).

**Handling**:
- Graceful degradation: preferences not persisted
- Display warning: "Browser storage full. Preferences won't be saved."
- Core functionality unaffected (auth uses cookies)

#### SSE Not Supported
**Scenario**: Browser doesn't support EventSource API.

**Handling**:
- Detect lack of support on initialization
- Fall back to polling for status updates
- Display message: "Live updates require modern browser. Using periodic refresh."

---

## 8. Accessibility and Usability Considerations

### WCAG AA Compliance

**Color Contrast**:
- All text meets 4.5:1 contrast ratio (normal text) and 3:1 (large text)
- MudBlazor custom palette verified for compliance
- Status colors include text labels, not color-only indicators

**Keyboard Navigation**:
- All interactive elements focusable and operable via keyboard
- Logical tab order matching visual layout
- Focus indicators visible on all focusable elements
- No keyboard traps in modal dialogs (Esc key closes)

**Screen Reader Support**:
- Semantic HTML structure (`<nav>`, `<main>`, `<article>`, `<section>`)
- ARIA labels for icon-only buttons
- ARIA live regions for dynamic content updates (SSE status, notifications)
- ARIA describedby for form validation messages
- Alt text for all images (if any added)

**Form Accessibility**:
- Label associations for all inputs
- Error messages linked via aria-describedby
- Required fields indicated visually and programmatically
- Field validation announced to screen readers

---

### Usability Heuristics

**Visibility of System Status**:
- Real-time status updates via SSE
- Loading states for all async operations
- Progress indicators for multi-step processes
- Clear feedback for user actions (success/error notifications)

**Match Between System and Real World**:
- Human-readable timestamps (relative + absolute)
- Plain language error messages
- Familiar icons and terminology
- RSS URL format explained with examples

**User Control and Freedom**:
- Undo for destructive actions (e.g., 5-second snackbar for feed deletion)
- Cancel buttons in all modal dialogs
- Back navigation in wizards
- Edit capability for all configurations

**Consistency and Standards**:
- Uniform component styling across views
- Consistent action placement (e.g., primary actions top-right)
- Standard navigation patterns
- Predictable error handling

**Error Prevention**:
- Client-side validation before API submission
- Confirmation dialogs for destructive actions
- Constraints enforced (e.g., minimum 1-hour schedule)
- Disabled buttons with tooltips explaining why

**Recognition Rather Than Recall**:
- Breadcrumbs show current location
- Preview items before feed creation
- Configuration summary in wizard review step
- Persistent navigation menu

**Flexibility and Efficiency**:
- Multiple view modes (table/grid)
- Filter presets for common queries
- Quick subscription links
- Optimistic UI for responsive feel

**Aesthetic and Minimalist Design**:
- Clean MudBlazor Material Design aesthetic
- Progressive disclosure of complex features
- No unnecessary information in primary views
- Expandable sections for advanced details

**Help Users with Errors**:
- RFC 7807 structured error display
- Actionable error messages with guidance
- Correlation IDs for support requests
- Contextual error explanations (e.g., why selector failed)

**Help and Documentation**:
- Inline tooltips for complex features
- Warnings explain implications clearly
- Password policy displayed during change
- RSS URL format explained with examples

---

### Performance Considerations

**Initial Load**:
- Blazor WebAssembly lazy loads assemblies
- Critical path optimized for fast render
- Skeleton loaders during data fetch

**Perceived Performance**:
- Optimistic UI updates
- Immediate feedback for interactions
- SSE reduces polling overhead
- Pagination limits data transfer

**Caching Strategy**:
- HTTP caching headers respected
- LocalStorage for preferences
- API responses cached via FusionCache (server-side)

**Bundle Size**:
- MudBlazor tree-shaken to reduce bundle
- Lazy load route-specific components
- Compress static assets (gzip/brotli)

---

### Mobile-First Responsive Design

**Breakpoints (MudBlazor)**:
- XS: <600px (phones)
- SM: 600-959px (tablets portrait)
- MD: 960-1279px (tablets landscape, small desktops)
- LG: 1280-1919px (desktops)
- XL: ≥1920px (large desktops)

**Mobile Adaptations**:
- Navigation drawer overlay mode
- Tables switch to card layouts
- Forms stack vertically
- Touch targets ≥44px
- Reduced data displayed (prioritize essential fields)

**Tablet Adaptations**:
- Hybrid drawer mode (overlay but wider)
- Tables show subset of columns
- Two-column card grids

**Desktop Optimizations**:
- Persistent navigation drawer
- Multi-column layouts
- Advanced filtering visible by default
- Keyboard shortcuts (future enhancement)

---

## 9. Security Considerations in UI

### Authentication Security

**Token Handling**:
- Cookies marked HttpOnly (no JavaScript access)
- Secure flag enforces HTTPS
- SameSite attribute prevents CSRF
- Access tokens never exposed in localStorage
- Refresh tokens managed server-side

**Session Management**:
- Automatic logout on token expiration
- No sensitive data persisted client-side
- Auth state cleared on logout
- Tab closure doesn't affect cookie session (configurable via Remember Me)

**Password Security**:
- Password inputs type=password (no visibility by default)
- No password client-side logging
- Password strength indicator encourages strong passwords
- Forced password change for default accounts

---

### Input Validation

**Client-Side Validation**:
- Email format validation
- URL format validation (HTTPS required)
- CSS selector syntax validation
- Schedule and TTL constraint validation

**Server-Side Validation**:
- All inputs validated server-side (client validation is convenience)
- FluentValidation rules enforce constraints
- RFC 7807 error responses for validation failures

**XSS Prevention**:
- Blazor auto-escapes all output
- Raw HTML excerpts displayed in code blocks (not rendered)
- User-generated content (titles, descriptions) sanitized
- No `[AllowHtml]` attributes on inputs

---

### Authorization

**Resource Ownership**:
- Feed and analysis detail pages verify ownership
- 403 Forbidden displayed for unauthorized access
- List endpoints filtered to user's resources only

**Role-Based Access**:
- Admin role required for system diagnostics (future)
- User role for feed operations
- Public role for RSS feed consumption

---

### CSRF Protection

**SameSite Cookies**:
- Authentication cookies use SameSite=Strict or Lax
- Prevents cross-site request forgery

**State Validation**:
- Login flow validates state parameter (if using OAuth, future)
- Form submissions include anti-forgery tokens (ASP.NET Core automatic)

---

### Content Security Policy

**Recommended Headers**:
- `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' https://api.rssvibe.com`
- Restrict script sources to prevent XSS
- Allow SSE connections to API domain

---

### Rate Limiting

**Client-Side Awareness**:
- Display rate limit errors (429 status)
- Countdown timers for cooldown periods
- Disable action buttons during cooldown
- Guidance: "Too many requests. Please wait X seconds."

**Server-Side Enforcement**:
- Analysis creation rate limited
- Manual parse trigger rate limited
- Login attempts rate limited

---

### Sensitive Data Handling

**No Logging**:
- Passwords never logged
- Access tokens not logged client-side
- Correlation IDs used for support (no sensitive data)

**Error Message Sanitization**:
- No stack traces displayed to users
- Generic error messages for security failures
- Technical details hidden by default (expandable)

---

## 10. Future Enhancements (Post-MVP)

While the following are excluded from MVP, the architecture accommodates future additions:

### Real-Time Collaboration
- Multi-user feed management
- Team workspaces
- Shared feed collections
- Activity feed showing team member actions

### Advanced Feed Customization
- Custom feed URLs (vanity URLs)
- Feed branding (logos, colors)
- Custom RSS namespaces
- Per-feed authentication

### Enhanced Analytics
- Feed performance metrics dashboard
- Item popularity tracking
- Parse success trends
- Source website health monitoring

### Content Enhancement
- Full article text extraction
- Readability improvements
- Image embedding in RSS items
- Audio/video enclosure support

### Automation
- IFTTT/Zapier integrations
- Webhook notifications on new items
- Email digests
- Scheduled reports

### User Preferences
- User profile page with avatar
- Notification preferences
- Email alert configuration
- Export/import feed collections

### Developer Features
- API key management for programmatic access
- Webhook configuration UI
- OPML import/export
- RSS to JSON API

---

## Conclusion

The RSSVibe UI architecture provides a comprehensive, accessible, and user-friendly interface for converting websites into RSS feeds. The design emphasizes:

1. **Progressive Disclosure**: Complex workflows broken into digestible steps
2. **Real-Time Feedback**: SSE integration eliminates uncertainty during long operations
3. **Accessibility**: WCAG AA compliance ensures usability for all users
4. **Responsive Design**: Mobile-first approach with graceful degradation
5. **Error Handling**: Structured error display with actionable guidance
6. **Security**: Cookie-based authentication with forced password change for default accounts
7. **Flexibility**: Multiple view modes, filtering, and sorting options
8. **Performance**: Optimistic UI, pagination, and caching strategies

All user stories from the PRD are fully covered, and the architecture is designed for extensibility to accommodate future enhancements beyond the MVP scope.
