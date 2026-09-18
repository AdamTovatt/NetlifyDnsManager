using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for <see cref="DnsUpdateService"/> DNS record update logic.
    /// </summary>
    [TestClass]
    public class DnsUpdateServiceTests
    {
        private const string Domain = "test.sakurapi.se";

        private Mock<INetlifyService> _netlifyServiceMock = null!;
        private DnsUpdateService _service = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _netlifyServiceMock = new Mock<INetlifyService>();
            _service = new DnsUpdateService(_netlifyServiceMock.Object, NullLogger<DnsUpdateService>.Instance);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenIpUnchanged_ReturnsFalseAndDoesNotUpdate()
        {
            // Arrange
            string currentIp = "1.2.3.4";

            SetUpZone(Domain, DnsRecordFactory.Create(Domain, "A", currentIp));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, currentIp);

            // Assert
            Assert.IsFalse(result);
            VerifyNoRecordDeleted();
            VerifyNoRecordAdded();
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenIpChanged_DeletesOldAndCreatesNew()
        {
            // Arrange
            string newIp = "5.6.7.8";
            NetlifyDnsRecord oldRecord = DnsRecordFactory.Create(Domain, "A", "1.2.3.4");

            SetUpZone(Domain, oldRecord);

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, newIp);

            // Assert
            Assert.IsTrue(result);
            VerifyDeleted(oldRecord);
            VerifyAdded(newIp);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenNoExistingRecord_CreatesNew()
        {
            // Arrange
            string ip = "1.2.3.4";

            SetUpZone(Domain);

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, ip);

            // Assert
            Assert.IsTrue(result);
            VerifyNoRecordDeleted();
            VerifyAdded(ip);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_IgnoresNonARecords()
        {
            // Arrange - only a CNAME record exists, no A record
            string ip = "1.2.3.4";

            SetUpZone(Domain, DnsRecordFactory.Create(Domain, "CNAME", "example.com"));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, ip);

            // Assert
            Assert.IsTrue(result);
            VerifyNoRecordDeleted();
            VerifyAdded(ip);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_IgnoresARecordsForDifferentHostname()
        {
            // Arrange - an A record exists but for a different hostname
            string ip = "1.2.3.4";

            SetUpZone(Domain, DnsRecordFactory.Create("other.sakurapi.se", "A", "9.9.9.9"));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, ip);

            // Assert
            Assert.IsTrue(result);
            VerifyNoRecordDeleted();
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenTheRecordIsNamedInAnotherCase_ReplacesItInsteadOfAddingASecond()
        {
            // Arrange - DNS names are case insensitive, so this is the record for the same name
            string newIp = "5.6.7.8";
            NetlifyDnsRecord existingRecord = DnsRecordFactory.Create(Domain.ToUpperInvariant(), "A", "1.2.3.4");

            SetUpZone(Domain, existingRecord);

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, newIp);

            // Assert - two A records at one name would answer half the lookups with the old address
            Assert.IsTrue(result);
            VerifyDeleted(existingRecord);
            VerifyAdded(newIp);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_LeavesAcmeChallengeRecordsAlone()
        {
            // Arrange - the reconciler runs while a certificate renewal has a challenge published
            string newIp = "5.6.7.8";
            NetlifyDnsRecord aRecord = DnsRecordFactory.Create(Domain, "A", "1.2.3.4");
            NetlifyDnsRecord challengeRecord = DnsRecordFactory.Create($"_acme-challenge.{Domain}", "TXT", "a-live-challenge-value");

            // The challenge record is listed first, so a lookup that is not exact picks it
            SetUpZone(Domain, challengeRecord, aRecord);

            // Act
            bool result = await _service.UpdateDnsRecordAsync(Domain, newIp);

            // Assert - only the A record is replaced, so the pending validation still finds its value
            Assert.IsTrue(result);
            VerifyDeleted(aRecord);
            VerifyNotDeleted(challengeRecord);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_ConcurrentCallsForOneDomain_DoNotBothCreateARecord()
        {
            // Arrange - hold the first call inside its zone read until the second one has started,
            // reading the zone as it is when each call starts
            TaskCompletionSource firstReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource releaseFirstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            List<NetlifyDnsRecord> zoneRecords = new List<NetlifyDnsRecord>();
            string ip = "1.2.3.4";
            int readCount = 0;

            _netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(Domain, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    List<NetlifyDnsRecord> zoneAtReadTime;

                    lock (zoneRecords)
                    {
                        zoneAtReadTime = zoneRecords.ToList();
                    }

                    if (Interlocked.Increment(ref readCount) == 1)
                    {
                        firstReadStarted.TrySetResult();
                        await releaseFirstRead.Task;
                    }

                    return new NetlifyDnsRecords(zoneAtReadTime);
                });

            _netlifyServiceMock.Setup(service => service.AddDnsRecordAsync(Domain, Domain, "A", ip, 1800, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    NetlifyDnsRecord added = DnsRecordFactory.Create(Domain, "A", ip);

                    lock (zoneRecords)
                    {
                        zoneRecords.Add(added);
                    }

                    return added;
                });

            // Act
            Task<bool> firstCall = _service.UpdateDnsRecordAsync(Domain, ip);
            await firstReadStarted.Task;
            Task<bool> secondCall = _service.UpdateDnsRecordAsync(Domain, ip);

            releaseFirstRead.TrySetResult();
            bool[] results = await Task.WhenAll(firstCall, secondCall);

            // Assert - the second call saw the first one's record, so it made no change of its own
            Assert.AreEqual(1, results.Count(updated => updated), "Exactly one of the two calls should have written a record.");
            VerifyAdded(ip);
        }

        private void SetUpZone(string domain, params NetlifyDnsRecord[] records)
        {
            _netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DnsRecordFactory.Zone(records));

            _netlifyServiceMock.Setup(service => service.AddDnsRecordAsync(
                    domain, domain, "A", It.IsAny<string>(), 1800, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string hostname, string zone, string type, string value, long ttl, CancellationToken _) =>
                    DnsRecordFactory.Create(hostname, type, value, ttl));
        }

        private void VerifyAdded(string ipAddress)
        {
            _netlifyServiceMock.Verify(
                service => service.AddDnsRecordAsync(Domain, Domain, "A", ipAddress, 1800, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private void VerifyDeleted(NetlifyDnsRecord record)
        {
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.Is<NetlifyDnsRecord>(deleted => deleted.Id == record.Id), It.IsAny<CancellationToken>()),
                Times.Once,
                $"Expected {record} to be deleted.");
        }

        private void VerifyNotDeleted(NetlifyDnsRecord record)
        {
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.Is<NetlifyDnsRecord>(deleted => deleted.Id == record.Id), It.IsAny<CancellationToken>()),
                Times.Never,
                $"Expected {record} to be left alone.");
        }

        private void VerifyNoRecordAdded()
        {
            _netlifyServiceMock.Verify(
                service => service.AddDnsRecordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private void VerifyNoRecordDeleted()
        {
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
