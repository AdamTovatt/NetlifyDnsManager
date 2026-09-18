using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Request model for publishing an ACME DNS-01 challenge value via the proxy API.
    /// The record name is derived from the domain by the server, so a client cannot name it.
    /// </summary>
    public class DnsChallengeRequest
    {
        /// <summary>
        /// The domain being validated, which must be one the client's key authorizes.
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// The challenge value to publish, as given by the ACME server.
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
