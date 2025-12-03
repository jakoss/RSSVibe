---
description: Implement API endpoint following the specification
agent: build
---
Your task is to implement a REST API endpoint based on the provided implementation plan. Your goal is to create a solid and well-organized implementation that includes appropriate validation, error handling, and follows all logical steps described in the plan.

You will be given a detailed implementation plan that includes all the necessary information to implement the endpoint. You will need to follow the rules described in the project AGENTS.md to ensure that the implementation is consistent with the project's architecture and design principles.

<api_implementation_plan>
The implementation plan for the endpoint is located in file $ARGUMENTS.
</api_implementation_plan>

Additionally, look at this additional information about the project context and implementation guidelines:
<types>
Contracts are defined in the project RSSVibe.Contracts.

Typed API clients are defined in RSSVibe.Contracts:
- Public interface: IXxxClient (e.g., IAuthClient, IFeedsClient, IFeedAnalysesClient)
- Internal implementation: XxxClient in Internal/ folder
- Registered in IRSSVibeApiClient and RSSVibeApiClient
- All endpoints MUST have corresponding typed client methods
</types>

<implementation_rules>
Follow the rules described in the project AGENTS.md.
</implementation_rules>

<implementation_approach>
Implement a maximum of 3 steps from the implementation plan, briefly summarize what you've done, and describe the plan for the next 3 actions - stop work at this point and wait for my feedback.
</implementation_approach>

Now perform the following steps to implement the REST API endpoint:

1. Analyze the implementation plan:
    - Determine the HTTP method (GET, POST, PUT, DELETE, etc.) for the endpoint.
    - Define the endpoint URL structure
    - List all expected input parameters
    - Understand the required business logic and data processing stages
    - Note any special requirements for validation or error handling.

2. Begin implementation:
    - Start by defining the endpoint function with the correct HTTP method decorator.
    - Configure function parameters based on expected inputs
    - Implement input validation for all parameters
    - Follow the logical steps described in the implementation plan
    - Implement error handling for each stage of the process
    - Ensure proper data processing and transformation according to requirements
    - Prepare the response data structure
    - Create typed API client method in appropriate IXxxClient interface
    - Implement typed API client method in XxxClient class
    - Register client in IRSSVibeApiClient if new client interface

3. API Client Implementation:
    - Add method signature to appropriate IXxxClient interface (e.g., IAuthClient, IFeedsClient)
    - Implement method in corresponding XxxClient class in Internal/ folder
    - Use HttpHelper.HandleResponseAsync<T>() for responses with data
    - Use HttpHelper.HandleResponseNoDataAsync() for 204 No Content responses
    - Build query strings using BuildQueryString() helper for GET requests
    - Return ApiResult<TData> or ApiResultNoData
    - If creating new client interface, register in IRSSVibeApiClient and RSSVibeApiClient
    
    Example for GET endpoint with query parameters:
    ```csharp
    // In IFeedsClient.cs
    Task<ApiResult<ListFeedsResponse>> ListAsync(
        ListFeedsRequest request,
        CancellationToken cancellationToken = default);
    
    // In FeedsClient.cs
    public async Task<ApiResult<ListFeedsResponse>> ListAsync(
        ListFeedsRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryParams = BuildQueryString(
            ("skip", request.Skip.ToString(CultureInfo.InvariantCulture)),
            ("take", request.Take.ToString(CultureInfo.InvariantCulture)),
            ("sort", request.Sort),
            ("status", request.Status)
        );
        
        var response = await httpClient.GetAsync(
            $"{BaseRoute}{queryParams}",
            cancellationToken);
        
        return await HttpHelper.HandleResponseAsync<ListFeedsResponse>(response, cancellationToken);
    }
    ```

4. Validation and error handling:
    - Implement thorough input validation for all parameters
    - Use appropriate HTTP status codes for different scenarios (e.g., 400 for bad requests, 404 for not found, 500 for server errors).
    - Provide clear and informative error messages in responses.
    - Handle potential exceptions that may occur during processing.

5. Testing considerations:
    - Consider edge cases and potential issues that should be tested.
    - Ensure the implementation covers all scenarios mentioned in the plan.
    - Implement integration tests for the endpoint using typed API clients.
    - CRITICAL: Tests MUST use ApiClient.Xxx.MethodAsync() (NOT HttpClient.GetAsync/PostAsync).
    - Ensure the new and existing tests pass.
    
    Example integration test using typed client:
    ```csharp
    [Test]
    public async Task ListFeeds_WithValidRequest_ShouldReturnPagedResults()
    {
        // Arrange
        var client = CreateAuthenticatedClient(); // For WebApplicationFactory
        var request = new ListFeedsRequest(
            Skip: 0,
            Take: 50,
            Sort: "lastParsedAt:desc",
            Status: null,
            Search: null
        );
        
        // Act - Use typed API client, NOT HttpClient.GetAsync()
        var result = await ApiClient.Feeds.ListAsync(request, CancellationToken.None);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Data).IsNotNull();
        await Assert.That(result.Data.Items).IsNotNull();
    }
    ```
    
    ❌ WRONG - Do NOT use raw HttpClient:
    ```csharp
    var response = await client.GetAsync("/api/v1/feeds?skip=0&take=50");
    ```
    
    ✅ CORRECT - Use typed API client:
    ```csharp
    var result = await ApiClient.Feeds.ListAsync(new ListFeedsRequest(...), ct);
    ```

6. Documentation:
    - Add clear comments to explain complex logic or important decisions
    - Include documentation for the main function and any helper functions.

After completing the implementation, ensure it includes all necessary imports, function definitions, and any additional helper functions or classes required for the implementation.

If you need to make any assumptions or have any questions about the implementation plan, present them before writing code.

Remember to follow REST API design best practices, adhere to programming language style guidelines, and ensure the code is clean, readable, and well-organized.
