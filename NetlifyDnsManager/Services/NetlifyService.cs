using NetlifyDnsManager.Models;
using System.Net.Http.Headers;
using System.Text;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Implementation of the Netlify service for managing DNS records.
    /// </summary>
    public class NetlifyService : INetlifyService
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetlifyService"/> class.
        /// </summary>
        /// <param name="accessToken">The Netlify access token for API authentication.</param>
        /// <param name="httpClient">The HttpClient instance to use for API requests.</param>
        public NetlifyService(string accessToken, HttpClient httpClient)
        {
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Retrieves all DNS records from Netlify for a specific domain.
        /// </summary>
        /// <param name="domain">The domain name to get DNS records for.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A collection of all DNS records.</returns>
        public async Task<NetlifyDnsRecords> GetAllDnsRecordsAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be null or empty.", nameof(domain));

            string zoneId = GetZoneIdFromHostname(ExtractDomainFromHostname(domain));
            string requestUrl = $"https://api.netlify.com/api/v1/dns_zones/{zoneId}/dns_records";

            HttpResponseMessage response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return NetlifyDnsRecords.FromJson(responseContent);
        }

        /// <summary>
        /// Adds a new DNS record to Netlify.
        /// </summary>
        /// <param name="hostname">The full hostname for the DNS record (e.g., "www.example.com", "api.example.com").</param>
        /// <param name="domain">The domain name for the DNS zone.</param>
        /// <param name="type">The type of DNS record (e.g., A, CNAME, MX, etc.).</param>
        /// <param name="value">The value of the DNS record.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>The created DNS record with updated information from Netlify.</returns>
        public async Task<NetlifyDnsRecord> AddDnsRecordAsync(string hostname, string domain, string type, string value, long ttl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentException("Hostname cannot be null or empty.", nameof(hostname));
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be null or empty.", nameof(domain));
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type cannot be null or empty.", nameof(type));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));

            string zoneId = GetZoneIdFromHostname(domain);
            string requestUrl = $"https://api.netlify.com/api/v1/dns_zones/{zoneId}/dns_records";

            NetlifyDnsRecord dnsRecord = new NetlifyDnsRecord(
                hostname: hostname,
                type: type,
                ttl: ttl,
                priority: null,
                weight: null,
                port: null,
                flag: null,
                tag: null,
                id: string.Empty,
                siteId: null,
                dnsZoneId: zoneId,
                errors: new List<object>(),
                managed: false,
                value: value);

            string jsonContent = dnsRecord.ToJson();
            StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return NetlifyDnsRecord.FromJson(responseContent);
        }

        /// <summary>
        /// Deletes a DNS record from Netlify.
        /// </summary>
        /// <param name="dnsRecord">The DNS record to delete.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        public async Task DeleteDnsRecordAsync(NetlifyDnsRecord dnsRecord, CancellationToken cancellationToken = default)
        {
            if (dnsRecord == null)
                throw new ArgumentNullException(nameof(dnsRecord));
            if (string.IsNullOrWhiteSpace(dnsRecord.Id))
                throw new ArgumentException("DNS record ID cannot be null or empty.", nameof(dnsRecord));

            string domain = ExtractDomainFromHostname(dnsRecord.Hostname);
            string zoneId = GetZoneIdFromHostname(domain);
            string requestUrl = $"https://api.netlify.com/api/v1/dns_zones/{zoneId}/dns_records/{dnsRecord.Id}";

            HttpResponseMessage response = await _httpClient.DeleteAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Gets the DNS zone ID from a hostname by replacing dots with underscores.
        /// </summary>
        /// <param name="hostname">The hostname to convert.</param>
        /// <returns>The DNS zone ID.</returns>
        private static string GetZoneIdFromHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentException("Hostname cannot be null or empty.", nameof(hostname));

            return hostname.Replace('.', '_');
        }

        /// <summary>
        /// Extracts the domain part from a full hostname by removing subdomains.
        /// </summary>
        /// <param name="hostname">The full hostname (e.g., "www.example.com", "api.example.com").</param>
        /// <returns>The domain part without subdomains (e.g., "example.com").</returns>
        private static string ExtractDomainFromHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentException("Hostname cannot be null or empty.", nameof(hostname));

            string[] parts = hostname.Split('.');
            if (parts.Length < 2)
                throw new ArgumentException("Hostname must contain at least a domain and TLD.", nameof(hostname));

            // For hostnames with 2 parts (e.g., "example.com"), return as is
            if (parts.Length == 2)
                return hostname;

            // For hostnames with more than 2 parts (e.g., "www.example.com"), 
            // return the last two parts as the domain
            return $"{parts[^2]}.{parts[^1]}";
        }
    }
}