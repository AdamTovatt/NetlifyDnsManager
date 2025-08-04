using EasyReasy.EnvironmentVariables;
using NetlifyDnsManager.Configuration;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            // Validate environment variables at startup
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(EnvironmentVariables));

            // Configure logging based on environment variable
            ConfigureLogging(builder);

            // Register services
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<INetlifyService>(serviceProvider =>
            {
                string accessToken = EnvironmentVariables.NetlifyAccessToken.GetValue();
                HttpClient httpClient = serviceProvider.GetRequiredService<HttpClient>();
                return new NetlifyService(accessToken, httpClient);
            });

            builder.Services.AddSingleton<IIpAddressService, CompoundIpAddressService>();
            builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

            builder.Services.AddHostedService<Worker>();

            IHost host = builder.Build();
            host.Run();
        }

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            try
            {
                string loggingValue = EnvironmentVariables.EnableLogging.GetValue();
                if (bool.TryParse(loggingValue, out bool enableLogging) && !enableLogging)
                {
                    // Disable all logging except errors when logging is set to false
                    builder.Logging.ClearProviders();
                    builder.Logging.AddConsole();
                    builder.Logging.SetMinimumLevel(LogLevel.Error);

                    // Specifically set HttpClient logging to Error level
                    builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Error);
                    builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Error);
                }
            }
            catch
            {
                // If ENABLE_LOGGING is not set, default to Information level
                builder.Logging.SetMinimumLevel(LogLevel.Information);
            }
        }
    }
}