using NetlifyDnsManager.Helpers;
using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service for updating DNS records. Used by both the local worker and the server API endpoint.
    /// </summary>
    public class DnsUpdateService : IDnsUpdateService
    {
        private readonly PerKeyLock _domainLocks = new PerKeyLock();
        private readonly INetlifyService _netlifyService;
        private readonly ILogger<DnsUpdateService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsUpdateService"/> class.
        /// </summary>
        /// <param name="netlifyService">The Netlify service for managing DNS records.</param>
        /// <param name="logger">The logger instance.</param>
        public DnsUpdateService(INetlifyService netlifyService, ILogger<DnsUpdateService> logger)
        {
            _netlifyService = netlifyService ?? throw new ArgumentNullException(nameof(netlifyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Updates the DNS A record for a domain to point to the specified IP address.
        /// If the record already points to the correct IP, no update is performed.
        /// Uses per-domain locking to prevent concurrent updates for the same domain.
        /// </summary>
        /// <param name="domain">The domain to update.</param>
        /// <param name="ipAddress">The IP address to set.</param>
        /// <param name="enableLogging">Whether to log informational messages.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>True if the record was updated, false if it was already current.</returns>
        public Task<bool> UpdateDnsRecordAsync(string domain, string ipAddress, bool enableLogging = true, CancellationToken cancellationToken = default)
        {
            return _domainLocks.RunAsync(domain, cancellationToken, () => UpdateDnsRecordInternalAsync(domain, ipAddress, enableLogging, cancellationToken));
        }

        private async Task<bool> UpdateDnsRecordInternalAsync(string domain, string ipAddress, bool enableLogging, CancellationToken cancellationToken)
        {
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(domain, cancellationToken);

            // Host names are matched without regard to case, because DNS names are case insensitive:
            // matching them exactly would add a second A record instead of replacing the first
            NetlifyDnsRecord? existingRecord = allRecords.Records.FirstOrDefault(r =>
                string.Equals(r.Hostname, domain, StringComparison.OrdinalIgnoreCase) && r.Type == "A");

            if (existingRecord != null)
            {
                if (existingRecord.Value == ipAddress)
                {
                    if (enableLogging)
                    {
                        _logger.LogInformation("Domain {Domain}: IP address is current ({IpAddress})", domain, ipAddress);
                    }

                    return false;
                }

                if (enableLogging)
                {
                    _logger.LogInformation("Domain {Domain}: IP address changed from {OldIp} to {NewIp}, updating",
                        domain, existingRecord.Value, ipAddress);
                }

                await _netlifyService.DeleteDnsRecordAsync(existingRecord, cancellationToken);
            }
            else
            {
                if (enableLogging)
                {
                    _logger.LogInformation("Domain {Domain}: No existing A record found, creating new one", domain);
                }
            }

            await _netlifyService.AddDnsRecordAsync(domain, domain, "A", ipAddress, 1800, cancellationToken);

            if (enableLogging)
            {
                _logger.LogInformation("Domain {Domain}: Created/updated A record with IP {IpAddress}", domain, ipAddress);
            }

            return true;
        }
    }
}
