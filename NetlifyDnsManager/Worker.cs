using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager
{
    /// <summary>
    /// Background service that manages DNS records for multiple domains.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly INetlifyService _netlifyService;
        private readonly IIpAddressService _ipAddressService;
        private readonly IConfigurationService _configurationService;

        public Worker(
            ILogger<Worker> logger,
            INetlifyService netlifyService,
            IIpAddressService ipAddressService,
            IConfigurationService configurationService)
        {
            _logger = logger;
            _netlifyService = netlifyService;
            _ipAddressService = ipAddressService;
            _configurationService = configurationService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ApplicationConfiguration configuration = _configurationService.GetConfiguration();

            if (configuration.EnableLogging)
            {
                _logger.LogInformation("Started Netlify DNS Manager");
                _logger.LogInformation("Configuration: CheckInterval={CheckInterval}s, Domains={Domains}",
                    configuration.CheckIntervalSeconds, string.Join(", ", configuration.Domains));
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndUpdateDnsRecordsAsync(configuration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during DNS check and update");
                }

                await Task.Delay(TimeSpan.FromSeconds(configuration.CheckIntervalSeconds), stoppingToken);
            }
        }

        private async Task CheckAndUpdateDnsRecordsAsync(ApplicationConfiguration configuration)
        {
            // Get current public IP address
            string currentIpAddress = await _ipAddressService.GetIpAddressAsync();

            if (configuration.EnableLogging)
            {
                _logger.LogInformation("Current IP address: {IpAddress}", currentIpAddress);
            }

            // Check and update each domain
            foreach (string domain in configuration.Domains)
            {
                if (configuration.EnableLogging)
                {
                    _logger.LogInformation("Checking domain: {Domain}", domain);
                }

                try
                {
                    await UpdateDomainDnsRecordAsync(domain, currentIpAddress, configuration.EnableLogging);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating DNS record for domain: {Domain}", domain);
                }
            }
        }

        private async Task UpdateDomainDnsRecordAsync(string domain, string currentIpAddress, bool enableLogging)
        {
            // Get all DNS records for the domain
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(domain);

            // Find existing A record for the domain
            NetlifyDnsRecord? existingRecord = allRecords.Records.FirstOrDefault(r =>
                r.Hostname == domain && r.Type == "A");

            if (existingRecord != null)
            {
                if (existingRecord.Value == currentIpAddress)
                {
                    if (enableLogging)
                    {
                        _logger.LogInformation("Domain {Domain}: IP address is current ({IpAddress})",
                            domain, currentIpAddress);
                    }

                    return; // No update needed
                }

                // IP address has changed, delete old record
                if (enableLogging)
                {
                    _logger.LogInformation("Domain {Domain}: IP address changed from {OldIp} to {NewIp}, updating",
                        domain, existingRecord.Value, currentIpAddress);
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

            // Create new A record
            NetlifyDnsRecord newRecord = await _netlifyService.AddDnsRecordAsync(
                domain, domain, "A", currentIpAddress, 1800);

            if (enableLogging)
            {
                _logger.LogInformation("Domain {Domain}: Created/updated A record with IP {IpAddress}",
                    domain, currentIpAddress);
            }
        }
    }
}
