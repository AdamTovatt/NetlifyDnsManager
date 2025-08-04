using Microsoft.Extensions.DependencyInjection;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Live integration tests for the IpAddressService using real external API.
    /// </summary>
    [TestClass]
    public class IpAddressServiceTests
    {
        private IIpAddressService _ipAddressService = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Set up dependency injection
            ServiceCollection services = new ServiceCollection();
            services.AddHttpClient();
            services.AddSingleton<IIpAddressService, CompoundIpAddressService>();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            _ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();
        }

        [TestMethod]
        public async Task GetIpAddressAsync_WithValidRequest_ReturnsValidIpAddress()
        {
            // Act
            string ipAddress = await _ipAddressService.GetIpAddressAsync();

            Console.WriteLine($"Retrieved IP address: {ipAddress}");

            // Assert
            Assert.IsNotNull(ipAddress);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ipAddress));

            // Validate that it's a valid IP address format
            string[] parts = ipAddress.Split('.');
            Assert.AreEqual(4, parts.Length, "IP address should have 4 octets");

            foreach (string part in parts)
            {
                Assert.IsTrue(int.TryParse(part, out int octet), $"Octet '{part}' should be a valid number");
                Assert.IsTrue(octet >= 0 && octet <= 255, $"Octet '{part}' should be between 0 and 255");
            }
        }

        [TestMethod]
        public async Task GetIpAddressAsync_MultipleCalls_ReturnsConsistentResult()
        {
            // Act
            string firstIpAddress = await _ipAddressService.GetIpAddressAsync();
            string secondIpAddress = await _ipAddressService.GetIpAddressAsync();

            Console.WriteLine($"First IP address: {firstIpAddress}");
            Console.WriteLine($"Second IP address: {secondIpAddress}");

            // Assert
            Assert.AreEqual(firstIpAddress, secondIpAddress, "Multiple calls should return the same IP address");
        }
    }
}