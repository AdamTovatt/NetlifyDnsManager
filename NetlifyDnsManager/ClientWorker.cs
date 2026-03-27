using EasyReasy.Auth.Client;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using System.Text;
using System.Text.Json;

namespace NetlifyDnsManager
{
    /// <summary>
    /// Background service that checks the local public IP and reports it to a proxy server.
    /// Used in <see cref="ProxyMode.Client"/> mode.
    /// </summary>
    public class ClientWorker : BackgroundService
    {
        private readonly ILogger<ClientWorker> _logger;
        private readonly IIpAddressService _ipAddressService;
        private readonly IConfigurationService _configurationService;
        private readonly AuthorizedHttpClient _authorizedHttpClient;
        private string? _lastReportedIp;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientWorker"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="ipAddressService">The IP address service for determining the public IP.</param>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="authorizedHttpClient">The authorized HTTP client for communicating with the proxy server.</param>
        public ClientWorker(
            ILogger<ClientWorker> logger,
            IIpAddressService ipAddressService,
            IConfigurationService configurationService,
            AuthorizedHttpClient authorizedHttpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ipAddressService = ipAddressService ?? throw new ArgumentNullException(nameof(ipAddressService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _authorizedHttpClient = authorizedHttpClient ?? throw new ArgumentNullException(nameof(authorizedHttpClient));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ApplicationConfiguration configuration = _configurationService.GetConfiguration();

            if (configuration.EnableLogging)
            {
                _logger.LogInformation("Started Netlify DNS Manager in client mode");
                _logger.LogInformation("Configuration: CheckInterval={CheckInterval}s, Server={ServerUrl}, Domains={Domains}",
                    configuration.CheckIntervalSeconds, configuration.ProxyServerUrl, string.Join(", ", configuration.Domains));
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndReportIpAsync(configuration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during IP check and report");
                }

                await Task.Delay(TimeSpan.FromSeconds(configuration.CheckIntervalSeconds), stoppingToken);
            }
        }

        private async Task CheckAndReportIpAsync(ApplicationConfiguration configuration)
        {
            string currentIpAddress = await _ipAddressService.GetIpAddressAsync();

            if (currentIpAddress == _lastReportedIp)
            {
                if (configuration.EnableLogging)
                {
                    _logger.LogInformation("IP address unchanged ({IpAddress}), skipping report", currentIpAddress);
                }

                return;
            }

            if (configuration.EnableLogging)
            {
                _logger.LogInformation("IP address changed from {OldIp} to {NewIp}, reporting to server",
                    _lastReportedIp ?? "(none)", currentIpAddress);
            }

            bool allSucceeded = true;

            foreach (string domain in configuration.Domains)
            {
                try
                {
                    await ReportDnsUpdateAsync(domain, currentIpAddress, configuration.EnableLogging);
                }
                catch (Exception ex)
                {
                    allSucceeded = false;
                    _logger.LogError(ex, "Error reporting DNS update for domain: {Domain}", domain);
                }
            }

            if (allSucceeded)
            {
                _lastReportedIp = currentIpAddress;
            }
        }

        private async Task ReportDnsUpdateAsync(string domain, string ipAddress, bool enableLogging)
        {
            DnsUpdateRequest request = new DnsUpdateRequest
            {
                Domain = domain,
                Ip = ipAddress
            };

            string json = JsonSerializer.Serialize(request);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _authorizedHttpClient.PostAsync("api/dns/update", content);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Server returned {response.StatusCode} for domain {domain}: {errorContent}");
            }

            if (enableLogging)
            {
                _logger.LogInformation("Domain {Domain}: Successfully reported IP {IpAddress} to server", domain, ipAddress);
            }
        }
    }
}
