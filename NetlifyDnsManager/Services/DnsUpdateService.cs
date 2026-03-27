using System.Collections.Concurrent;
using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service for updating DNS records. Used by both the local worker and the server API endpoint.
    /// </summary>
    public class DnsUpdateService : IDnsUpdateService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _domainLocks = new();
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
        /// <returns>True if the record was updated, false if it was already current.</returns>
        public async Task<bool> UpdateDnsRecordAsync(string domain, string ipAddress, bool enableLogging = true)
        {
            SemaphoreSlim domainLock = _domainLocks.GetOrAdd(domain, _ => new SemaphoreSlim(1, 1));
            await domainLock.WaitAsync();

            try
            {
                return await UpdateDnsRecordInternalAsync(domain, ipAddress, enableLogging);
            }
            finally
            {
                domainLock.Release();
            }
        }

        private async Task<bool> UpdateDnsRecordInternalAsync(string domain, string ipAddress, bool enableLogging)
        {
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(domain);

            NetlifyDnsRecord? existingRecord = allRecords.Records.FirstOrDefault(r =>
                r.Hostname == domain && r.Type == "A");

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

                await _netlifyService.DeleteDnsRecordAsync(existingRecord);
            }
            else
            {
                if (enableLogging)
                {
                    _logger.LogInformation("Domain {Domain}: No existing A record found, creating new one", domain);
                }
            }

            await _netlifyService.AddDnsRecordAsync(domain, domain, "A", ipAddress, 1800);

            if (enableLogging)
            {
                _logger.LogInformation("Domain {Domain}: Created/updated A record with IP {IpAddress}", domain, ipAddress);
            }

            return true;
        }
    }
}
