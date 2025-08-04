using EasyReasy.EnvironmentVariables;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Environment variable configuration for tests.
    /// </summary>
    [EnvironmentVariableNameContainer]
    public static class TestEnvironmentVariables
    {
        /// <summary>
        /// Netlify access token for API authentication.
        /// </summary>
        [EnvironmentVariableName(minLength: 20)]
        public static readonly VariableName NetlifyAccessToken = new VariableName("NETLIFY_ACCESS_TOKEN");

        /// <summary>
        /// The test domain to use for DNS record operations.
        /// </summary>
        [EnvironmentVariableName(minLength: 5)]
        public static readonly VariableName TestDomain = new VariableName("TEST_DOMAIN");
    }
}