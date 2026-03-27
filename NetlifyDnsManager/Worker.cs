using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager
{
    /// <summary>
    /// Background service that manages DNS records for multiple domains.
    /// Used in <see cref="ProxyMode.None"/> and <see cref="ProxyMode.Server"/> modes.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IIpAddressService _ipAddressService;
        private readonly IDnsUpdateService _dnsUpdateService;
        private readonly IConfigurationService _configurationService;

        public Worker(
            ILogger<Worker> logger,
            IIpAddressService ipAddressService,
            IDnsUpdateService dnsUpdateService,
            IConfigurationService configurationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ipAddressService = ipAddressService ?? throw new ArgumentNullException(nameof(ipAddressService));
            _dnsUpdateService = dnsUpdateService ?? throw new ArgumentNullException(nameof(dnsUpdateService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
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
            string currentIpAddress = await _ipAddressService.GetIpAddressAsync();

            if (configuration.EnableLogging)
            {
                _logger.LogInformation("Current IP address: {IpAddress}", currentIpAddress);
            }

            foreach (string domain in configuration.Domains)
            {
                if (configuration.EnableLogging)
                {
                    _logger.LogInformation("Checking domain: {Domain}", domain);
                }

                try
                {
                    await _dnsUpdateService.UpdateDnsRecordAsync(domain, currentIpAddress, configuration.EnableLogging);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating DNS record for domain: {Domain}", domain);
                }
            }
        }
    }
}
