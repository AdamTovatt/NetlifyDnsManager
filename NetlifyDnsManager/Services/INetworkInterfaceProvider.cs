using System.Net;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Provides read access to the IPv4 addresses assigned to the host's network interfaces.
    /// </summary>
    public interface INetworkInterfaceProvider
    {
        /// <summary>
        /// Tries to get the IPv4 addresses currently assigned to the network interface with the given name.
        /// </summary>
        /// <param name="interfaceName">The name of the network interface (e.g. "eth0", "wg0", "tailscale0").</param>
        /// <param name="addresses">
        /// The IPv4 addresses assigned to the interface, empty if the interface has none.
        /// </param>
        /// <returns>True if an interface with that name exists on this host, false otherwise.</returns>
        bool TryGetIpv4Addresses(string interfaceName, out IReadOnlyList<IPAddress> addresses);
    }
}
