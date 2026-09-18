using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Reads network interface addresses from the operating system.
    /// </summary>
    public class SystemNetworkInterfaceProvider : INetworkInterfaceProvider
    {
        /// <summary>
        /// Tries to get the IPv4 addresses currently assigned to the network interface with the given name.
        /// Interface names are compared case-insensitively.
        /// </summary>
        /// <param name="interfaceName">The name of the network interface (e.g. "eth0", "wg0", "tailscale0").</param>
        /// <param name="addresses">
        /// The IPv4 addresses assigned to the interface, empty if the interface has none.
        /// </param>
        /// <returns>True if an interface with that name exists on this host, false otherwise.</returns>
        public bool TryGetIpv4Addresses(string interfaceName, out IReadOnlyList<IPAddress> addresses)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));

            NetworkInterface? networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(candidate => string.Equals(candidate.Name, interfaceName, StringComparison.OrdinalIgnoreCase));

            if (networkInterface == null)
            {
                addresses = Array.Empty<IPAddress>();
                return false;
            }

            addresses = networkInterface.GetIPProperties().UnicastAddresses
                .Select(unicastAddress => unicastAddress.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            return true;
        }
    }
}
