using NetlifyDnsManager.Helpers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Represents a collection of DNS records from Netlify.
    /// </summary>
    public class NetlifyDnsRecords
    {
        /// <summary>
        /// Gets or sets the list of DNS records.
        /// </summary>
        [JsonPropertyName("records")]
        public List<NetlifyDnsRecord> Records { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetlifyDnsRecords"/> class.
        /// </summary>
        /// <param name="records">The list of DNS records.</param>
        public NetlifyDnsRecords(List<NetlifyDnsRecord> records)
        {
            Records = records;
        }

        /// <summary>
        /// Creates a <see cref="NetlifyDnsRecords"/> from JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A new <see cref="NetlifyDnsRecords"/> instance.</returns>
        /// <exception cref="JsonException">Thrown when the JSON cannot be deserialized.</exception>
        public static NetlifyDnsRecords FromJson(string json)
        {
            List<NetlifyDnsRecord> records = JsonSerializer.Deserialize<List<NetlifyDnsRecord>>(json, JsonSettings.Default) ??
                throw new JsonException($"Error when deserializing {nameof(NetlifyDnsRecords)} from json: {json}");

            return new NetlifyDnsRecords(records);
        }

        /// <summary>
        /// Finds the first A record with the specified domain name.
        /// </summary>
        /// <param name="domainName">The domain name to search for.</param>
        /// <returns>The first matching A record, or null if not found.</returns>
        internal NetlifyDnsRecord? First(string domainName)
        {
            if (Records == null || Records.Count == 0)
                return null;

            return Records.Where(x => x.Type == "A" && x.Hostname == domainName).FirstOrDefault();
        }

        public override string ToString()
        {
            return string.Join('\n', Records);
        }
    }
}
