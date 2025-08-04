using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service interface for managing Netlify DNS records.
    /// </summary>
    public interface INetlifyService
    {
        /// <summary>
        /// Retrieves all DNS records from Netlify for a specific domain.
        /// </summary>
        /// <param name="domain">The domain name to get DNS records for.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A collection of all DNS records.</returns>
        Task<NetlifyDnsRecords> GetAllDnsRecordsAsync(string domain, CancellationToken cancellationToken = default);

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
        Task<NetlifyDnsRecord> AddDnsRecordAsync(string hostname, string domain, string type, string value, long ttl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a DNS record from Netlify.
        /// </summary>
        /// <param name="dnsRecord">The DNS record to delete.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        Task DeleteDnsRecordAsync(NetlifyDnsRecord dnsRecord, CancellationToken cancellationToken = default);
    }
}