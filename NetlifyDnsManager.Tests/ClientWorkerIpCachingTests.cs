using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using System.Text.Json;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the client worker's IP caching behaviour against the real worker:
    /// it only reports to the server when the address it reads has changed.
    /// </summary>
    [TestClass]
    public class ClientWorkerIpCachingTests
    {
        private const int CheckIntervalSeconds = 1;

        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);

        private int _addressReadCount;
        private Mock<IIpAddressService> _ipAddressServiceMock = null!;
        private RecordingHttpHandler _handler = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _addressReadCount = 0;
            _ipAddressServiceMock = new Mock<IIpAddressService>();
            _handler = new RecordingHttpHandler();
        }

        [TestMethod]
        public async Task IpCaching_WhenIpUnchanged_ReportsOnlyOnce()
        {
            // Arrange
            SetUpAddresses("1.2.3.4", "1.2.3.4", "1.2.3.4");

            // Act - let three cycles read the same address
            await RunCyclesAsync(3);

            // Assert
            Assert.AreEqual(1, _handler.UpdateRequests.Count, $"Sent: {string.Join(", ", _handler.UpdateRequests)}");
            Assert.AreEqual("1.2.3.4", ReportedAddress(0));
        }

        [TestMethod]
        public async Task IpCaching_WhenIpChanges_ReportsTheNewAddress()
        {
            // Arrange
            SetUpAddresses("1.2.3.4", "5.6.7.8");

            // Act
            await RunCyclesAsync(2);

            // Assert
            Assert.AreEqual(2, _handler.UpdateRequests.Count, $"Sent: {string.Join(", ", _handler.UpdateRequests)}");
            Assert.AreEqual("1.2.3.4", ReportedAddress(0));
            Assert.AreEqual("5.6.7.8", ReportedAddress(1));
        }

        [TestMethod]
        public async Task IpCaching_FirstCycleAlwaysReports()
        {
            // Arrange
            SetUpAddresses("1.2.3.4");

            // Act
            await RunCyclesAsync(1);

            // Assert
            Assert.AreEqual(1, _handler.UpdateRequests.Count);
            Assert.AreEqual("1.2.3.4", ReportedAddress(0));
        }

        [TestMethod]
        public async Task IpCaching_IpChangesBackAndForth_ReportsEachChange()
        {
            // Arrange
            SetUpAddresses("1.2.3.4", "5.6.7.8", "1.2.3.4");

            // Act
            await RunCyclesAsync(3);

            // Assert
            Assert.AreEqual(3, _handler.UpdateRequests.Count, $"Sent: {string.Join(", ", _handler.UpdateRequests)}");
            Assert.AreEqual("1.2.3.4", ReportedAddress(0));
            Assert.AreEqual("5.6.7.8", ReportedAddress(1));
            Assert.AreEqual("1.2.3.4", ReportedAddress(2));
        }

        [TestMethod]
        public async Task IpCaching_WhenAReportFails_TheNextCycleReportsAgain()
        {
            // Arrange - the server rejects the first report, so the address was never accepted
            SetUpAddresses("1.2.3.4", "1.2.3.4");
            _handler.FailUpdateRequests = true;

            // Act
            await RunCyclesAsync(2);

            // Assert - an unchanged address is retried after a failure rather than assumed published
            Assert.AreEqual(2, _handler.UpdateRequests.Count, $"Sent: {string.Join(", ", _handler.UpdateRequests)}");
        }

        private void SetUpAddresses(params string[] addresses)
        {
            Queue<string> remainingAddresses = new Queue<string>(addresses);
            string lastAddress = addresses[^1];

            _ipAddressServiceMock.Setup(service => service.GetIpAddressAsync())
                .Callback(() => Interlocked.Increment(ref _addressReadCount))
                .ReturnsAsync(() => remainingAddresses.Count > 0 ? remainingAddresses.Dequeue() : lastAddress);
        }

        private async Task RunCyclesAsync(int cycleCount)
        {
            ClientWorker worker = ClientWorkerFactory.Create(_ipAddressServiceMock.Object, _handler, CheckIntervalSeconds);

            await worker.StartAsync(CancellationToken.None);

            bool allCyclesRan = await TestWait.UntilAsync(
                () => Volatile.Read(ref _addressReadCount) >= cycleCount,
                WaitTimeout);

            await worker.StopAsync(CancellationToken.None);

            Assert.IsTrue(allCyclesRan, $"Only {Volatile.Read(ref _addressReadCount)} of {cycleCount} cycles ran.");
        }

        private string ReportedAddress(int updateRequestIndex)
        {
            DnsUpdateRequest? reported = JsonSerializer.Deserialize<DnsUpdateRequest>(_handler.UpdateRequests[updateRequestIndex].Body);

            Assert.IsNotNull(reported);
            Assert.AreEqual(ClientWorkerFactory.Domain, reported!.Domain);

            return reported.Ip;
        }
    }
}
