using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service interface for loading and managing application configuration.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the current application configuration.
        /// </summary>
        /// <returns>The application configuration.</returns>
        ApplicationConfiguration GetConfiguration();
    }
}