namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Configuration settings for the Netlify DNS Manager application.
    /// </summary>
    public class ApplicationConfiguration
    {
        /// <summary>
        /// Gets or sets the list of domain names to manage DNS records for.
        /// </summary>
        public List<string> Domains { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the interval in seconds between DNS checks and updates.
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 1800; // Default: 30 minutes

        /// <summary>
        /// Gets or sets whether console logging is enabled.
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the Netlify access token for API authentication.
        /// Required in <see cref="ProxyMode.None"/> and <see cref="ProxyMode.Server"/> modes.
        /// </summary>
        public string NetlifyAccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the proxy mode for this instance.
        /// </summary>
        public ProxyMode ProxyMode { get; set; } = ProxyMode.None;

        /// <summary>
        /// Gets or sets the port for the web API in server mode.
        /// </summary>
        public int ApiPort { get; set; } = 5050;

        /// <summary>
        /// Gets or sets the JWT secret for signing tokens in server mode.
        /// </summary>
        public string JwtSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the clients configuration file in server mode.
        /// </summary>
        public string ClientsConfigPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL of the proxy server in client mode.
        /// </summary>
        public string ProxyServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the API key for the proxy server in client mode.
        /// </summary>
        public string ProxyApiKey { get; set; } = string.Empty;
    }
}
