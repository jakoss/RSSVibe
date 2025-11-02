// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for <see cref="IHostEnvironment"/>.
/// </summary>
public static class HostEnvironmentExtensions
{
    private const string _integrationTestsEnvironment = "IntegrationTests";

    /// <summary>
    /// Checks if the current host environment name is "IntegrationTests".
    /// </summary>
    /// <param name="hostEnvironment">An instance of <see cref="IHostEnvironment"/>.</param>
    /// <returns>True if the environment name is "IntegrationTests"; otherwise, false.</returns>
    public static bool IsIntegrationTests(this IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        return hostEnvironment.IsEnvironment(_integrationTestsEnvironment);
    }
}
