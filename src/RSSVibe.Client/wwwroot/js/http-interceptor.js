// HTTP Interceptor to include credentials (cookies) in all cross-origin fetch requests
// This is necessary for Blazor WASM because the managed HttpClient doesn't automatically
// include cookies when making requests to different origins.

export function initHttpInterceptor() {
    const originalFetch = window.fetch;

    window.fetch = function(...args) {
        // Get the URL from the first argument
        let url = args[0];
        if (url instanceof Request) {
            url = url.url;
        }

        // Get or create the options object (second argument)
        let options = args[1] || {};

        // Always include credentials so cookies are sent with requests
        // This is safe because we have SameSite=None + Secure + Domain set
        if (!options.credentials) {
            options.credentials = 'include';
        }

        // Call the original fetch with modified options
        return originalFetch.apply(this, [args[0], options]);
    };
}
