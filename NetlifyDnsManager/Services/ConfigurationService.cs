using EasyReasy.EnvironmentVariables;
using NetlifyDnsManager.Configuration;
using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Implementation of the configuration service that loads settings from environment variables.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        /// <summary>
        /// Gets the current application configuration from environment variables.
        /// </summary>
        /// <returns>The application configuration.</returns>
        public ApplicationConfiguration GetConfiguration()
        {
            ApplicationConfiguration configuration = new ApplicationConfiguration
            {
                // Load Netlify access token
                NetlifyAccessToken = EnvironmentVariables.NetlifyAccessToken.GetValue(),

                // Load all domains from the range
                Domains = EnvironmentVariables.Domains.GetAllValues(),

                // Load check interval (default to 1800 seconds if not specified)
                CheckIntervalSeconds = GetCheckInterval(),

                // Load logging setting (default to true if not specified)
                EnableLogging = GetEnableLogging()
            };

            return configuration;
        }

        /// <summary>
        /// Gets the check interval from environment variables with a default fallback.
        /// </summary>
        /// <returns>The check interval in seconds.</returns>
        private static int GetCheckInterval()
        {
            try
            {
                string intervalValue = EnvironmentVariables.CheckInterval.GetValue();
                if (int.TryParse(intervalValue, out int interval))
                {
                    return interval;
                }
            }
            catch
            {
                // Use default if not specified or invalid
            }

            return 1800; // Default: 30 minutes
        }

        /// <summary>
        /// Gets the logging setting from environment variables with a default fallback.
        /// </summary>
        /// <returns>True if logging is enabled, false otherwise.</returns>
        private static bool GetEnableLogging()
        {
            try
            {
                string loggingValue = EnvironmentVariables.EnableLogging.GetValue();
                if (bool.TryParse(loggingValue, out bool enableLogging))
                {
                    return enableLogging;
                }
            }
            catch
            {
                // Use default if not specified or invalid
            }

            return true; // Default: enable logging
        }
    }
} 