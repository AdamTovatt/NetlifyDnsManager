using EasyReasy.Auth.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Builds a real <see cref="ClientWorker"/> whose only fakes are the address source and the
    /// HTTP transport, so tests observe what the worker itself does.
    /// </summary>
    internal static class ClientWorkerFactory
    {
        /// <summary>
        /// The domain the created worker reports for.
        /// </summary>
        public const string Domain = "host.example.com";

        /// <summary>
        /// Creates a client worker reporting to the given handler.
        /// </summary>
        /// <param name="ipAddressService">The address source the worker reads from.</param>
        /// <param name="handler">The handler that records what the worker sends.</param>
        /// <param name="checkIntervalSeconds">The interval between the worker's check cycles.</param>
        /// <returns>The created worker.</returns>
        public static ClientWorker Create(IIpAddressService ipAddressService, RecordingHttpHandler handler, int checkIntervalSeconds = 600)
        {
            HttpClient httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5050/")
            };

            AuthorizedHttpClient authorizedHttpClient = new AuthorizedHttpClient(httpClient, "test-api-key");

            ApplicationConfiguration configuration = new ApplicationConfiguration
            {
                ProxyMode = ProxyMode.Client,
                Domains = new List<string> { Domain },
                CheckIntervalSeconds = checkIntervalSeconds,
                EnableLogging = false,
                ProxyServerUrl = "http://localhost:5050/",
                ProxyApiKey = "test-api-key"
            };

            Mock<IConfigurationService> configurationServiceMock = new Mock<IConfigurationService>();
            configurationServiceMock.Setup(service => service.GetConfiguration()).Returns(configuration);

            return new ClientWorker(
                NullLogger<ClientWorker>.Instance,
                ipAddressService,
                configurationServiceMock.Object,
                authorizedHttpClient);
        }
    }
}
