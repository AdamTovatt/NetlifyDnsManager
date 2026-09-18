namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Registers the single service used to determine the address this host reports.
    /// </summary>
    public static class IpAddressServiceRegistration
    {
        /// <summary>
        /// Registers the address source configured by the IP_SOURCE_INTERFACE environment variable.
        /// </summary>
        /// <param name="services">The service collection to register the service in.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddIpAddressService(this IServiceCollection services)
        {
            return services.AddIpAddressService(ConfigurationService.GetIpSourceInterfaceName());
        }

        /// <summary>
        /// Registers exactly one <see cref="IIpAddressService"/>: the interface reader when an interface
        /// name is given, and the public IP services when it is null or blank.
        /// </summary>
        /// <remarks>
        /// The two sources are mutually exclusive, which is what keeps the interface reader's lack of a
        /// fallback meaningful. See <see cref="NetworkInterfaceIpAddressService"/> for why that matters.
        /// </remarks>
        /// <param name="services">The service collection to register the service in.</param>
        /// <param name="interfaceName">The name of the interface to read the address from, or null to use the public IP services.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddIpAddressService(this IServiceCollection services, string? interfaceName)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                services.AddSingleton<IIpAddressService, CompoundIpAddressService>();
            }
            else
            {
                string trimmedInterfaceName = interfaceName.Trim();

                services.AddSingleton<IIpAddressService>(
                    _ => new NetworkInterfaceIpAddressService(trimmedInterfaceName, new SystemNetworkInterfaceProvider()));
            }

            return services;
        }
    }
}
