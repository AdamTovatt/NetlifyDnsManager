using System.Net;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Reports the IPv4 address of a named local network interface instead of the host's public IP address.
    /// Used for hosts whose useful address is private, such as a host reachable over a VPN or on a LAN.
    /// </summary>
    /// <remarks>
    /// This service has no fallback by design. If the configured interface cannot be read, it throws,
    /// which means no address is reported for that cycle. Falling back to another source would publish
    /// the host's public address and then correct itself on a later cycle, leaving a hostname that
    /// resolves publicly some of the time while looking correct whenever it is checked. For the same
    /// reason it replaces the other address sources rather than being registered alongside them, which
    /// is what <see cref="IpAddressServiceRegistration.AddIpAddressService(IServiceCollection, string?)"/> guarantees.
    /// </remarks>
    public class NetworkInterfaceIpAddressService : IIpAddressService
    {
        private readonly string _interfaceName;
        private readonly INetworkInterfaceProvider _networkInterfaceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkInterfaceIpAddressService"/> class.
        /// </summary>
        /// <param name="interfaceName">The name of the network interface to read the address from.</param>
        /// <param name="networkInterfaceProvider">The provider used to read interface addresses.</param>
        public NetworkInterfaceIpAddressService(string interfaceName, INetworkInterfaceProvider networkInterfaceProvider)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));

            _interfaceName = interfaceName;
            _networkInterfaceProvider = networkInterfaceProvider ?? throw new ArgumentNullException(nameof(networkInterfaceProvider));
        }

        /// <summary>
        /// Gets a description of where this service reads the address from.
        /// </summary>
        public string Description => $"network interface {_interfaceName}";

        /// <summary>
        /// Gets the IPv4 address of the configured network interface. If the interface has several
        /// IPv4 addresses, the first usable one the operating system lists is returned.
        /// </summary>
        /// <returns>The IPv4 address of the configured interface as a string.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configured interface does not exist, has no IPv4 address assigned, or has
        /// only a link-local address. Callers must treat this as "no address available" and not report anything.
        /// </exception>
        public Task<string> GetIpAddressAsync()
        {
            if (!_networkInterfaceProvider.TryGetIpv4Addresses(_interfaceName, out IReadOnlyList<IPAddress> addresses))
                throw new InvalidOperationException($"No network interface named '{_interfaceName}' exists on this host.");

            if (addresses.Count == 0)
                throw new InvalidOperationException($"Network interface '{_interfaceName}' has no IPv4 address assigned.");

            IPAddress? address = addresses.FirstOrDefault(candidate => !IsLinkLocal(candidate));

            if (address == null)
            {
                throw new InvalidOperationException(
                    $"Network interface '{_interfaceName}' has only link-local IPv4 addresses ({string.Join(", ", addresses)}), " +
                    "which means it never received a real address.");
            }

            return Task.FromResult(address.ToString());
        }

        /// <summary>
        /// Determines whether an address is an IPv4 link-local address, which an interface assigns
        /// itself when address configuration fails and which is useless to publish.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns>True if the address is in 169.254.0.0/16, false otherwise.</returns>
        private static bool IsLinkLocal(IPAddress address)
        {
            byte[] octets = address.GetAddressBytes();

            return octets[0] == 169 && octets[1] == 254;
        }
    }
}
