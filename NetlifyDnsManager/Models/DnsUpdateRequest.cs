using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Request model for updating a DNS record via the proxy API.
    /// </summary>
    public class DnsUpdateRequest
    {
        /// <summary>
        /// The domain to update the DNS record for.
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// The IP address to set for the domain.
        /// </summary>
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;
    }
}
