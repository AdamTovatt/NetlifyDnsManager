using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests that the worker used in none and server mode writes no DNS record when the address
    /// source cannot produce an address.
    /// </summary>
    [TestClass]
    public class WorkerAddressUnavailableTests
    {
        private const string Domain = "host.example.com";
        private const string InterfaceAddress = "10.1.1.7";

        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        private Mock<IDnsUpdateService> _dnsUpdateServiceMock = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _dnsUpdateServiceMock = new Mock<IDnsUpdateService>();
        }

        [TestMethod]
        public async Task Worker_WhenAddressSourceFails_UpdatesNoDnsRecord()
        {
            // Arrange
            CountingAddressSource addressSource = CountingAddressSource.Failing(
                new InvalidOperationException("Network interface 'wg0' has no IPv4 address assigned."));

            Worker worker = CreateWorker(addressSource);

            // Act
            await worker.StartAsync(CancellationToken.None);
            bool cycleRan = await addressSource.WaitForReadsAsync(1, WaitTimeout);
            await worker.StopAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(cycleRan, "The worker never read the address source, so this test proves nothing.");
            _dnsUpdateServiceMock.Verify(
                service => service.UpdateDnsRecordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task Worker_WhenAddressIsAvailable_UpdatesTheDnsRecordWithThatAddress()
        {
            // Arrange - the same harness with a working source, to prove the absence above is meaningful
            CountingAddressSource addressSource = CountingAddressSource.Returning(InterfaceAddress);

            int updateCount = 0;
            _dnsUpdateServiceMock
                .Setup(service => service.UpdateDnsRecordAsync(Domain, InterfaceAddress, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => Interlocked.Increment(ref updateCount))
                .ReturnsAsync(true);

            Worker worker = CreateWorker(addressSource);

            // Act
            await worker.StartAsync(CancellationToken.None);
            await TestWait.UntilAsync(() => Volatile.Read(ref updateCount) > 0, WaitTimeout);
            await worker.StopAsync(CancellationToken.None);

            // Assert - the domain and the address reach the update service in those positions
            _dnsUpdateServiceMock.Verify(
                service => service.UpdateDnsRecordAsync(Domain, InterfaceAddress, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        private Worker CreateWorker(IIpAddressService ipAddressService)
        {
            ApplicationConfiguration configuration = new ApplicationConfiguration
            {
                ProxyMode = ProxyMode.None,
                Domains = new List<string> { Domain },
                CheckIntervalSeconds = 600,
                EnableLogging = false
            };

            Mock<IConfigurationService> configurationServiceMock = new Mock<IConfigurationService>();
            configurationServiceMock.Setup(service => service.GetConfiguration()).Returns(configuration);

            return new Worker(
                NullLogger<Worker>.Instance,
                ipAddressService,
                _dnsUpdateServiceMock.Object,
                configurationServiceMock.Object);
        }
    }
}
