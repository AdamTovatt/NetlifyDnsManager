namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service interface for updating DNS records.
    /// </summary>
    public interface IDnsUpdateService
    {
        /// <summary>
        /// Updates the DNS A record for a domain to point to the specified IP address.
        /// </summary>
        /// <param name="domain">The domain to update.</param>
        /// <param name="ipAddress">The IP address to set.</param>
        /// <param name="enableLogging">Whether to log informational messages.</param>
        /// <returns>True if the record was updated, false if it was already current.</returns>
        Task<bool> UpdateDnsRecordAsync(string domain, string ipAddress, bool enableLogging = true);
    }
}
