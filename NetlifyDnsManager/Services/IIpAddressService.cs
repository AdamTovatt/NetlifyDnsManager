namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service interface for retrieving IP address information.
    /// </summary>
    public interface IIpAddressService
    {
        /// <summary>
        /// Gets a description of where this service reads the address from, for logging.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the IP address this host should publish.
        /// </summary>
        /// <returns>The IP address as a string.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no address could be determined. Implementations must throw rather than return a
        /// substitute address, and callers must then skip the update instead of publishing anything.
        /// </exception>
        Task<string> GetIpAddressAsync();
    }
}
