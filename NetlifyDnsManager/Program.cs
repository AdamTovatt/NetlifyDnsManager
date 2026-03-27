using EasyReasy.Auth;
using EasyReasy.Auth.Client;
using EasyReasy.EnvironmentVariables;
using NetlifyDnsManager.Configuration;
using NetlifyDnsManager.Endpoints;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Determine proxy mode early (before validation) since it affects which env vars are required
            ProxyMode proxyMode = ConfigurationService.GetProxyMode();

            switch (proxyMode)
            {
                case ProxyMode.None:
                    RunNoneMode(args);
                    break;
                case ProxyMode.Server:
                    RunServerMode(args);
                    break;
                case ProxyMode.Client:
                    RunClientMode(args);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(proxyMode), proxyMode, $"Unsupported proxy mode: {proxyMode}");
            }
        }

        /// <summary>
        /// Runs in the default mode: checks own IP and updates Netlify directly.
        /// This is the original behavior of the application.
        /// </summary>
        private static void RunNoneMode(string[] args)
        {
            // Validate required environment variables upfront
            string accessToken = EnvironmentVariables.NetlifyAccessToken.GetValue();
            EnvironmentVariables.Domains.GetAllValues();

            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            ConfigureLogging(builder.Logging);
            RegisterNetlifyServices(builder.Services, accessToken);
            builder.Services.AddHostedService<Worker>();

            IHost host = builder.Build();
            host.Run();
        }

        /// <summary>
        /// Runs in server mode: does everything the default mode does, plus hosts a web API
        /// that accepts DNS update requests from authenticated proxy clients.
        /// </summary>
        private static void RunServerMode(string[] args)
        {
            // Validate required environment variables upfront
            string accessToken = EnvironmentVariables.NetlifyAccessToken.GetValue();
            string jwtSecret = EnvironmentVariables.JwtSecret.GetValue();
            string clientsConfigPath = EnvironmentVariables.ClientsConfigPath.GetValue();
            EnvironmentVariables.Domains.GetAllValues();
            int apiPort = ConfigurationService.GetApiPort();

            // Load and register clients configuration
            ClientsConfiguration clientsConfiguration = ClientsConfiguration.FromFile(clientsConfigPath);

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            ConfigureLogging(builder.Logging);

            builder.Services.AddSingleton(clientsConfiguration);

            // Configure Kestrel to listen on the specified port
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(apiPort);
            });

            // Register core services
            RegisterNetlifyServices(builder.Services, accessToken);

            // Register auth services
            builder.Services.AddEasyReasyAuth(jwtSecret);
            builder.Services.AddSingleton<IAuthRequestValidationService, ClientAuthValidationService>();

            // Register the background worker (same as None mode)
            builder.Services.AddHostedService<Worker>();

            WebApplication app = builder.Build();

            // Configure middleware pipeline
            app.UseEasyReasyAuth();
            app.AddAuthEndpoints(allowApiKeys: true, allowUsernamePassword: false);
            app.MapDnsUpdateEndpoints();

            app.Run();
        }

        /// <summary>
        /// Runs in client mode: checks own IP and reports it to a remote proxy server
        /// instead of updating Netlify directly.
        /// </summary>
        private static void RunClientMode(string[] args)
        {
            // Validate required environment variables upfront
            string proxyServerUrl = EnvironmentVariables.ProxyServerUrl.GetValue();
            string proxyApiKey = EnvironmentVariables.ProxyApiKey.GetValue();
            EnvironmentVariables.Domains.GetAllValues();

            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            ConfigureLogging(builder.Logging);

            RegisterCommonServices(builder.Services);

            // Register the authorized HTTP client for communicating with the proxy server
            builder.Services.AddSingleton(serviceProvider =>
            {
                HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient(proxyServerUrl);
                return new AuthorizedHttpClient(httpClient, proxyApiKey);
            });

            builder.Services.AddHostedService<ClientWorker>();

            IHost host = builder.Build();
            host.Run();
        }

        /// <summary>
        /// Registers services common to all modes: HTTP client, IP address service, and configuration service.
        /// </summary>
        /// <param name="services">The service collection to register services in.</param>
        private static void RegisterCommonServices(IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<IIpAddressService, CompoundIpAddressService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
        }

        /// <summary>
        /// Registers Netlify-specific services on top of common services.
        /// Used by None and Server modes which interact with the Netlify API directly.
        /// </summary>
        /// <param name="services">The service collection to register services in.</param>
        /// <param name="netlifyAccessToken">The Netlify access token for API authentication.</param>
        private static void RegisterNetlifyServices(IServiceCollection services, string netlifyAccessToken)
        {
            RegisterCommonServices(services);

            services.AddSingleton<INetlifyService>(serviceProvider =>
            {
                HttpClient httpClient = serviceProvider.GetRequiredService<HttpClient>();
                return new NetlifyService(netlifyAccessToken, httpClient);
            });

            services.AddSingleton<IDnsUpdateService, DnsUpdateService>();
        }

        private static void ConfigureLogging(ILoggingBuilder logging)
        {
            try
            {
                string loggingValue = EnvironmentVariables.EnableLogging.GetValue();
                if (bool.TryParse(loggingValue, out bool enableLogging) && !enableLogging)
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Error);
                    logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Error);
                    logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Error);
                }
            }
            catch (Exception)
            {
                // If ENABLE_LOGGING is not set, default to Information level
                logging.SetMinimumLevel(LogLevel.Information);
            }
        }
    }
}
