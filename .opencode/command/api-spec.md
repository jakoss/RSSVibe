---
description: Generate API endpoint implementation plan from endpoint specification
agent: build
---
You are an experienced software architect whose task is to create a detailed implementation plan for a REST API endpoint. Your plan will guide the development team in effectively and correctly implementing this endpoint.

<endpoint_specifications>
The user will provide one or more endpoint specifications from .ai/api-plan.md that need implementation plans created. Each specification includes the route, description, request/response format, and status codes.

Endpoint specifications to generate plans for:
$ARGUMENTS
</endpoint_specifications>

Additionally, review the following additional information:

1. Related database resources:
   <related_db_resources>
   Read the database models from @.ai/db-plan.md to understand entity structure, relationships, and constraints.
   </related_db_resources>

2. Type definitions:
   <type_definitions>
   All contract types are declared in RSSVibe.Contracts project with directory structure matching the endpoint name.
   </type_definitions>

3. Tech stack:
   <tech_stack>
   Read the tech stack from @.ai/tech-stack.md to understand framework versions, libraries, and architectural patterns.
   </tech_stack>

4. Implementation rules:
   <implementation_rules>
   Follow the implementation rules from AGENTS.md, including:
   - Minimal API patterns with TypedResults
   - Service layer architecture with command/result patterns
   - FluentValidation for request validation
   - Entity Framework Core with strongly-typed JSON properties
   - ASP.NET Core Identity for authentication
   - Hierarchical endpoint organization (MapGroup pattern)
   </implementation_rules>

Your task is to create a comprehensive implementation plan for each specified REST API endpoint. Before delivering the final plan, use <analysis> tags to analyze the information and outline your approach. In this analysis, ensure that:

1. Summarize key points of the API specification.
2. List required and optional parameters from the API specification.
3. List necessary DTO types (Request/Response contracts) and Command/Result models for the service layer.
4. Consider how to extract logic to a service (existing or new, if it doesn't exist).
5. Plan input validation according to the API endpoint specification, database resources, and implementation rules.
6. Determine appropriate logging strategy for errors and audit events.
7. Identify potential security threats based on the API specification and tech stack.
8. Outline potential error scenarios and corresponding status codes.

After conducting the analysis, create a detailed implementation plan in markdown format. The plan should contain the following sections:

1. Endpoint Overview
2. Request Details
3. Used Types
4. Response Details
5. Data Flow
6. Security Considerations
7. Error Handling
8. Performance Considerations
9. Implementation Steps

Throughout the plan, ensure that you:
- Use correct API status codes:
    - 200 for successful read
    - 201 for successful creation with Location header
    - 202 for accepted async operations with Location header
    - 204 for successful delete/update with no content
    - 400 for invalid input
    - 401 for unauthorized access
    - 403 for forbidden access (authenticated but insufficient permissions)
    - 404 for not found resources
    - 409 for conflict (duplicate resource, concurrent modification)
    - 422 for unprocessable entity (validation passed but business rules failed)
    - 429 for rate limiting
    - 503 for service unavailable
- Adapt to the provided tech stack
- Follow the provided implementation rules

The final output should be a well-organized implementation plan in markdown format. Here's the expected structure:

```markdown
# API Endpoint Implementation Plan: [Endpoint Name]

## 1. Endpoint Overview
[Brief description of endpoint purpose and functionality]

## 2. Request Details
- HTTP Method: [GET/POST/PUT/PATCH/DELETE]
- URL Structure: [URL pattern]
- Parameters:
    - Path: [path parameters]
    - Query: [query parameters]
    - Body: [request body structure, if applicable]
- Authentication: [Required/Optional and authorization requirements]

## 3. Used Types

### Request Contracts
[DTOs for request (positional records in RSSVibe.Contracts)]

### Response Contracts
[DTOs for response (positional records in RSSVibe.Contracts)]

### Service Layer Types
[Command/Result types for service layer in RSSVibe.Services]

### Validation Rules
[FluentValidation rules as nested Validator class]

## 4. Response Details
[Expected response structure and status codes with TypedResults]

## 5. Data Flow
1. [Step-by-step description of data flow]
2. [Include interactions with services, database, external APIs]
3. [Mention transaction boundaries if applicable]

## 6. Security Considerations
- Authentication: [JWT bearer token requirements]
- Authorization: [Role/policy requirements, resource ownership checks]
- Input Validation: [Validation strategy]
- Rate Limiting: [If applicable]
- Additional Security: [HTTPS, CORS, etc.]

## 7. Error Handling
| Scenario | Status Code | Response |
|----------|-------------|----------|
| [Error scenario] | [Code] | [Response structure] |

## 8. Performance Considerations
- [Database query optimization (AsNoTracking, indexes, Include)]
- [Caching strategy (FusionCache if applicable)]
- [Pagination approach]
- [Potential bottlenecks]

## 9. Implementation Steps
1. **Create Contract Types** - Define request/response DTOs in RSSVibe.Contracts
2. **Create Service Layer** - Define command/result types and service interface/implementation
3. **Implement Validation** - Create FluentValidation validator as nested class
4. **Create Endpoint** - Implement minimal API endpoint with TypedResults
5. **Register Endpoint** - Add to appropriate MapGroup hierarchy
6. **Add Tests** - Create integration tests covering all scenarios
7. **Update Documentation** - Add OpenAPI metadata
```

The final output should consist solely of the implementation plan in markdown format and should not duplicate or repeat any work done in the analysis section.

Remember to save your implementation plan as `.ai/implementation-plans/<path_to_endpoint>.md`. The file path should mirror the endpoint structure (e.g., `/api/v1/auth/register` → `.ai/implementation-plans/auth/register.md`).

For each endpoint specification provided by the user:
1. Perform analysis in <analysis> tags
2. Generate complete implementation plan
3. Save to appropriate location in `.ai/implementation-plans/`
4. Provide summary of what was created

Ensure the plan is detailed, clear, and provides comprehensive guidance for the development team.
