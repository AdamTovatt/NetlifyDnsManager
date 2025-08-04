using System.Net;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Implementation of the IP address service for retrieving public IP addresses.
    /// </summary>
    public class IpAddressService : IIpAddressService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="IpAddressService"/> class.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for API requests.</param>
        /// <param name="baseUrl">The base URL for the IP address service (e.g., "https://icanhazip.com").</param>
        public IpAddressService(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        /// <summary>
        /// Gets the current public IP address.
        /// </summary>
        /// <returns>The public IP address as a string.</returns>
        public async Task<string> GetIpAddressAsync()
        {
            try
            {
                string response = await _httpClient.GetStringAsync(_baseUrl);
                string ipAddress = response.Replace("\r\n", "").Replace("\n", "").Trim();

                // Validate that the response is a valid IP address
                if (IPAddress.TryParse(ipAddress, out IPAddress? parsedAddress))
                {
                    return ipAddress;
                }

                throw new InvalidOperationException($"Invalid IP address received: {ipAddress}");
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Failed to retrieve IP address from external service.", ex);
            }
        }
    }
}