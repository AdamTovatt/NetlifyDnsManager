using EasyReasy.EnvironmentVariables;

namespace NetlifyDnsManager.Configuration
{
    /// <summary>
    /// Environment variables configuration for the Netlify DNS Manager application.
    /// </summary>
    [EnvironmentVariableNameContainer]
    public static class EnvironmentVariables
    {
        /// <summary>
        /// Netlify access token for API authentication.
        /// </summary>
        [EnvironmentVariableName(minLength: 20)]
        public static readonly VariableName NetlifyAccessToken = new VariableName("NETLIFY_ACCESS_TOKEN");

        /// <summary>
        /// Range of domain names to manage DNS records for.
        /// Supports DOMAIN_01, DOMAIN_02, etc.
        /// </summary>
        [EnvironmentVariableNameRange(minCount: 1)]
        public static readonly VariableNameRange Domains = new VariableNameRange("DOMAIN");

        /// <summary>
        /// Interval in seconds between DNS checks and updates.
        /// </summary>
        [EnvironmentVariableName]
        public static readonly VariableName CheckInterval = new VariableName("CHECK_INTERVAL");

        /// <summary>
        /// Whether to enable console logging.
        /// </summary>
        [EnvironmentVariableName]
        public static readonly VariableName EnableLogging = new VariableName("ENABLE_LOGGING");
    }
}