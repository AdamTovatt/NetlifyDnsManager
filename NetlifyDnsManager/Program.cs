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
    }
}