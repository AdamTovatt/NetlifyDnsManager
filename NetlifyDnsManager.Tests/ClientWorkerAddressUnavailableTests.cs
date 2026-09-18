using NetlifyDnsManager.Models;
using System.Text.Json;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests that the client reports nothing at all when the address source cannot produce an address,
    /// rather than reporting some other address.
    /// </summary>
    [TestClass]
    public class ClientWorkerAddressUnavailableTests
    {
        private const string InterfaceAddress = "10.1.1.7";

        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        [TestMethod]
        public async Task ClientWorker_WhenAddressSourceFails_SendsNothingAtAll()
        {
            // Arrange - the interface read failed, exactly as the interface reader reports it
            CountingAddressSource addressSource = CountingAddressSource.Failing(
                new InvalidOperationException("Network interface 'wg0' has no IPv4 address assigned."));

            RecordingHttpHandler handler = new RecordingHttpHandler();
            ClientWorker worker = ClientWorkerFactory.Create(addressSource, handler);

            // Act - wait for the cycle to read the address, then see what it sent
            await worker.StartAsync(CancellationToken.None);
            bool cycleRan = await addressSource.WaitForReadsAsync(1, WaitTimeout);
            await worker.StopAsync(CancellationToken.None);

            // Assert - the cycle ran, and it sent nothing: no update, and not even an authentication attempt
            Assert.IsTrue(cycleRan, "The worker never read the address source, so this test proves nothing.");
            Assert.AreEqual(0, handler.Requests.Count, $"Expected no requests, got: {string.Join(", ", handler.Requests)}");
        }

        [TestMethod]
        public async Task ClientWorker_WhenAddressIsAvailable_ReportsThatAddress()
        {
            // Arrange - the same harness with a working source, to prove the absence above is meaningful
            CountingAddressSource addressSource = CountingAddressSource.Returning(InterfaceAddress);

            RecordingHttpHandler handler = new RecordingHttpHandler();
            ClientWorker worker = ClientWorkerFactory.Create(addressSource, handler);

            // Act
            await worker.StartAsync(CancellationToken.None);
            await TestWait.UntilAsync(() => handler.UpdateRequests.Count > 0, WaitTimeout);
            await worker.StopAsync(CancellationToken.None);

            // Assert - the private address is reported for the configured domain, in those fields
            Assert.AreEqual(1, handler.UpdateRequests.Count, $"Expected one update request, got: {string.Join(", ", handler.Requests)}");

            DnsUpdateRequest? reported = JsonSerializer.Deserialize<DnsUpdateRequest>(handler.UpdateRequests[0].Body);

            Assert.IsNotNull(reported);
            Assert.AreEqual(InterfaceAddress, reported!.Ip);
            Assert.AreEqual(ClientWorkerFactory.Domain, reported.Domain);
        }
    }
}
