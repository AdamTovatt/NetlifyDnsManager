using Microsoft.Extensions.DependencyInjection;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Compound IP address service that uses multiple IP address services for redundancy.
    /// </summary>
    public class CompoundIpAddressService : IIpAddressService
    {
        private readonly IIpAddressService[] _ipAddressServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompoundIpAddressService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider to create individual IP address services.</param>
        public CompoundIpAddressService(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            // Create three different IP address services with different base URLs
            _ipAddressServices = new IIpAddressService[]
            {
                new IpAddressService(serviceProvider.GetRequiredService<HttpClient>(), "https://icanhazip.com"),
                new IpAddressService(serviceProvider.GetRequiredService<HttpClient>(), "https://api.ipify.org"),
                new IpAddressService(serviceProvider.GetRequiredService<HttpClient>(), "https://ipv4.seeip.org")
            };
        }

        /// <summary>
        /// Gets the current public IP address by trying multiple services concurrently.
        /// </summary>
        /// <returns>The public IP address as a string.</returns>
        public async Task<string> GetIpAddressAsync()
        {
            // Start all requests concurrently
            Task<string>[] tasks = _ipAddressServices.Select(service => service.GetIpAddressAsync()).ToArray();

            // Wait for the first successful response
            while (tasks.Length > 0)
            {
                Task<string> completedTask = await Task.WhenAny(tasks);
                
                try
                {
                    string ipAddress = await completedTask;
                    
                    // Validate that we got a valid IP address
                    if (!string.IsNullOrWhiteSpace(ipAddress) && IsValidIpAddress(ipAddress))
                    {
                        return ipAddress;
                    }
                }
                catch (Exception)
                {
                    // Continue with remaining tasks
                }

                // Remove the completed task from the array
                tasks = tasks.Where(t => t != completedTask).ToArray();
            }

            // If all services failed, throw an exception
            throw new InvalidOperationException("All IP address services failed.");
        }

        /// <summary>
        /// Validates if a string is a valid IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address string to validate.</param>
        /// <returns>True if the IP address is valid, false otherwise.</returns>
        private static bool IsValidIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            string[] parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (string part in parts)
            {
                if (!int.TryParse(part, out int octet) || octet < 0 || octet > 255)
                    return false;
            }

            return true;
        }
    }
} 