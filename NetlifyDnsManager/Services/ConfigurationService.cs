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
        /// The default API port used when no port is configured.
        /// </summary>
        internal const int DefaultApiPort = 5050;

        private readonly ILogger<ConfigurationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public ConfigurationService(ILogger<ConfigurationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current application configuration from environment variables.
        /// </summary>
        /// <returns>The application configuration.</returns>
        public ApplicationConfiguration GetConfiguration()
        {
            ProxyMode proxyMode = GetProxyMode();
            List<string> domains = GetDomains();

            if (domains.Count == 0)
            {
                _logger.LogWarning("No domains configured. Set DOMAIN_01, DOMAIN_02, etc. environment variables.");
            }

            ApplicationConfiguration configuration = new ApplicationConfiguration
            {
                ProxyMode = proxyMode,
                CheckIntervalSeconds = GetCheckInterval(),
                EnableLogging = GetEnableLogging(),
                Domains = domains
            };

            switch (proxyMode)
            {
                case ProxyMode.None:
                    configuration.NetlifyAccessToken = EnvironmentVariables.NetlifyAccessToken.GetValue();
                    break;

                case ProxyMode.Server:
                    configuration.NetlifyAccessToken = EnvironmentVariables.NetlifyAccessToken.GetValue();
                    configuration.ApiPort = GetApiPort();
                    configuration.JwtSecret = EnvironmentVariables.JwtSecret.GetValue();
                    configuration.ClientsConfigPath = EnvironmentVariables.ClientsConfigPath.GetValue();
                    break;

                case ProxyMode.Client:
                    configuration.ProxyServerUrl = EnvironmentVariables.ProxyServerUrl.GetValue();
                    configuration.ProxyApiKey = EnvironmentVariables.ProxyApiKey.GetValue();
                    break;
            }

            return configuration;
        }

        /// <summary>
        /// Gets the proxy mode from environment variables.
        /// </summary>
        /// <returns>The proxy mode. Defaults to <see cref="ProxyMode.None"/> if not set or invalid.</returns>
        internal static ProxyMode GetProxyMode()
        {
            try
            {
                string modeValue = EnvironmentVariables.ProxyMode.GetValue();
                if (Enum.TryParse<ProxyMode>(modeValue, ignoreCase: true, out ProxyMode mode))
                {
                    return mode;
                }
            }
            catch (Exception)
            {
                // Use default if not specified
            }

            return ProxyMode.None;
        }

        /// <summary>
        /// Gets the domains from environment variables.
        /// </summary>
        /// <returns>The list of domains.</returns>
        private static List<string> GetDomains()
        {
            try
            {
                return EnvironmentVariables.Domains.GetAllValues();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets the API port from environment variables with a default fallback.
        /// Validates that the port is in the valid range (1-65535).
        /// </summary>
        /// <returns>The API port number.</returns>
        internal static int GetApiPort()
        {
            try
            {
                string portValue = EnvironmentVariables.ApiPort.GetValue();
                if (int.TryParse(portValue, out int port) && port >= 1 && port <= 65535)
                {
                    return port;
                }
            }
            catch (Exception)
            {
                // Use default if not specified or invalid
            }

            return DefaultApiPort;
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
            catch (Exception)
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
            catch (Exception)
            {
                // Use default if not specified or invalid
            }

            return true; // Default: enable logging
        }
    }
}
