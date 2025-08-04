namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service interface for retrieving IP address information.
    /// </summary>
    public interface IIpAddressService
    {
        /// <summary>
        /// Gets the current public IP address.
        /// </summary>
        /// <returns>The public IP address as a string.</returns>
        Task<string> GetIpAddressAsync();
    }
}