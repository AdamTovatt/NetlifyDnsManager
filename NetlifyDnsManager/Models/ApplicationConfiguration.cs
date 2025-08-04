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
        /// </summary>
        public required string NetlifyAccessToken { get; set; }
    }
} 