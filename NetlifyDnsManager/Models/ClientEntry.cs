using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Represents a single authorized client with its API key and allowed domains.
    /// </summary>
    public class ClientEntry
    {
        /// <summary>
        /// The API key for this client.
        /// </summary>
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// The list of domains this client is authorized to update.
        /// </summary>
        [JsonPropertyName("allowedDomains")]
        public List<string> AllowedDomains { get; set; } = new List<string>();

        /// <summary>
        /// An optional display name for this client.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
