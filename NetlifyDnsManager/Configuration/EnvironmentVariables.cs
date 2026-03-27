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
        /// Required in <see cref="Models.ProxyMode.None"/> and <see cref="Models.ProxyMode.Server"/> modes.
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

        /// <summary>
        /// The proxy mode to run in. Valid values: "none", "server", "client".
        /// If not set, defaults to "none" (standard behavior).
        /// </summary>
        [EnvironmentVariableName]
        public static readonly VariableName ProxyMode = new VariableName("PROXY_MODE");

        /// <summary>
        /// The port for the web API to listen on in server mode.
        /// </summary>
        [EnvironmentVariableName]
        public static readonly VariableName ApiPort = new VariableName("API_PORT");

        /// <summary>
        /// The JWT secret used for signing tokens in server mode.
        /// Must be at least 32 bytes (256 bits) for HS256.
        /// </summary>
        [EnvironmentVariableName(minLength: 32)]
        public static readonly VariableName JwtSecret = new VariableName("JWT_SECRET");

        /// <summary>
        /// The path to the clients configuration JSON file in server mode.
        /// </summary>
        [EnvironmentVariableName]
        public static readonly VariableName ClientsConfigPath = new VariableName("CLIENTS_CONFIG_PATH");

        /// <summary>
        /// The URL of the proxy server to send DNS updates to in client mode.
        /// </summary>
        [EnvironmentVariableName]
        public static readonly VariableName ProxyServerUrl = new VariableName("PROXY_SERVER_URL");

        /// <summary>
        /// The API key for authenticating with the proxy server in client mode.
        /// </summary>
        [EnvironmentVariableName(minLength: 10)]
        public static readonly VariableName ProxyApiKey = new VariableName("PROXY_API_KEY");
    }
}
