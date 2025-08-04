using NetlifyDnsManager.Helpers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Represents a DNS record in Netlify.
    /// </summary>
    public class NetlifyDnsRecord
    {
        /// <summary>
        /// The hostname for the DNS record.
        /// </summary>
        [JsonPropertyName("hostname")]
        public string Hostname { get; set; }

        /// <summary>
        /// The type of DNS record (e.g., A, CNAME, MX, etc.).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Time to live value for the DNS record in seconds.
        /// </summary>
        [JsonPropertyName("ttl")]
        public long Ttl { get; set; }

        /// <summary>
        /// Priority value for the DNS record (used for MX records).
        /// </summary>
        [JsonPropertyName("priority")]
        public object? Priority { get; set; }

        /// <summary>
        /// Weight value for the DNS record (used for SRV records).
        /// </summary>
        [JsonPropertyName("weight")]
        public object? Weight { get; set; }

        /// <summary>
        /// Port value for the DNS record (used for SRV records).
        /// </summary>
        [JsonPropertyName("port")]
        public object? Port { get; set; }

        /// <summary>
        /// Flag value for the DNS record (used for CAA records).
        /// </summary>
        [JsonPropertyName("flag")]
        public object? Flag { get; set; }

        /// <summary>
        /// Tag value for the DNS record (used for CAA records).
        /// </summary>
        [JsonPropertyName("tag")]
        public object? Tag { get; set; }

        /// <summary>
        /// Unique identifier for the DNS record.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The site ID associated with this DNS record.
        /// </summary>
        [JsonPropertyName("site_id")]
        public object? SiteId { get; set; }

        /// <summary>
        /// The DNS zone ID for this record.
        /// </summary>
        [JsonPropertyName("dns_zone_id")]
        public string DnsZoneId { get; set; }

        /// <summary>
        /// List of errors associated with this DNS record.
        /// </summary>
        [JsonPropertyName("errors")]
        public List<object> Errors { get; set; }

        /// <summary>
        /// Indicates whether this DNS record is managed by Netlify.
        /// </summary>
        [JsonPropertyName("managed")]
        public bool Managed { get; set; }

        /// <summary>
        /// The value of the DNS record (e.g., IP address for A records).
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetlifyDnsRecord"/> class.
        /// </summary>
        /// <param name="hostname">The hostname for the DNS record.</param>
        /// <param name="type">The type of DNS record.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
        /// <param name="priority">Priority value for the DNS record.</param>
        /// <param name="weight">Weight value for the DNS record.</param>
        /// <param name="port">Port value for the DNS record.</param>
        /// <param name="flag">Flag value for the DNS record.</param>
        /// <param name="tag">Tag value for the DNS record.</param>
        /// <param name="id">Unique identifier for the DNS record.</param>
        /// <param name="siteId">The site ID associated with this DNS record.</param>
        /// <param name="dnsZoneId">The DNS zone ID for this record.</param>
        /// <param name="errors">List of errors associated with this DNS record.</param>
        /// <param name="managed">Indicates whether this DNS record is managed by Netlify.</param>
        /// <param name="value">The value of the DNS record.</param>
        public NetlifyDnsRecord(
            string hostname,
            string type,
            long ttl,
            object? priority,
            object? weight,
            object? port,
            object? flag,
            object? tag,
            string id,
            object? siteId,
            string dnsZoneId,
            List<object> errors,
            bool managed,
            string value)
        {
            Hostname = hostname;
            Type = type;
            Ttl = ttl;
            Priority = priority;
            Weight = weight;
            Port = port;
            Flag = flag;
            Tag = tag;
            Id = id;
            SiteId = siteId;
            DnsZoneId = dnsZoneId;
            Errors = errors;
            Managed = managed;
            Value = value;
        }

        /// <summary>
        /// Creates a <see cref="NetlifyDnsRecord"/> from JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A new <see cref="NetlifyDnsRecord"/> instance.</returns>
        /// <exception cref="JsonException">Thrown when the JSON cannot be deserialized or results in a null object.</exception>
        public static NetlifyDnsRecord FromJson(string json)
        {
            NetlifyDnsRecord? result = JsonSerializer.Deserialize<NetlifyDnsRecord>(json, JsonSettings.Default);

            if (result == null)
                throw new JsonException($"Null object result when deserializing {nameof(NetlifyDnsRecord)} from json: {json}");

            return result;
        }

        /// <summary>
        /// Serializes this <see cref="NetlifyDnsRecord"/> to JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this DNS record.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSettings.Default);
        }

        /// <summary>
        /// Returns the value of the DNS record as a string representation.
        /// </summary>
        /// <returns>The value of the DNS record.</returns>
        public override string ToString()
        {
            return $"{Hostname} ({Ttl} {Type}) {Value}";
        }
    }
}
