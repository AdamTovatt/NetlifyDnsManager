using Microsoft.Extensions.Logging;
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
        private Mock<INetlifyService> _netlifyServiceMock = null!;
        private Mock<ILogger<DnsUpdateService>> _loggerMock = null!;
        private DnsUpdateService _service = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _netlifyServiceMock = new Mock<INetlifyService>();
            _loggerMock = new Mock<ILogger<DnsUpdateService>>();
            _service = new DnsUpdateService(_netlifyServiceMock.Object, _loggerMock.Object);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenIpUnchanged_ReturnsFalseAndDoesNotUpdate()
        {
            // Arrange
            string domain = "test.sakurapi.se";
            string currentIp = "1.2.3.4";

            _netlifyServiceMock.Setup(s => s.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecords(domain, "A", currentIp));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(domain, currentIp);

            // Assert
            Assert.IsFalse(result);
            _netlifyServiceMock.Verify(s => s.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            _netlifyServiceMock.Verify(s => s.AddDnsRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenIpChanged_DeletesOldAndCreatesNew()
        {
            // Arrange
            string domain = "test.sakurapi.se";
            string oldIp = "1.2.3.4";
            string newIp = "5.6.7.8";

            _netlifyServiceMock.Setup(s => s.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecords(domain, "A", oldIp));

            _netlifyServiceMock.Setup(s => s.AddDnsRecordAsync(domain, domain, "A", newIp, 1800, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecord(domain, "A", newIp));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(domain, newIp);

            // Assert
            Assert.IsTrue(result);
            _netlifyServiceMock.Verify(s => s.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()), Times.Once);
            _netlifyServiceMock.Verify(s => s.AddDnsRecordAsync(domain, domain, "A", newIp, 1800, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_WhenNoExistingRecord_CreatesNew()
        {
            // Arrange
            string domain = "new.sakurapi.se";
            string ip = "1.2.3.4";

            _netlifyServiceMock.Setup(s => s.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NetlifyDnsRecords(new List<NetlifyDnsRecord>()));

            _netlifyServiceMock.Setup(s => s.AddDnsRecordAsync(domain, domain, "A", ip, 1800, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecord(domain, "A", ip));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(domain, ip);

            // Assert
            Assert.IsTrue(result);
            _netlifyServiceMock.Verify(s => s.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            _netlifyServiceMock.Verify(s => s.AddDnsRecordAsync(domain, domain, "A", ip, 1800, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_IgnoresNonARecords()
        {
            // Arrange
            string domain = "test.sakurapi.se";
            string ip = "1.2.3.4";

            // Only a CNAME record exists, no A record
            NetlifyDnsRecords records = new NetlifyDnsRecords(new List<NetlifyDnsRecord>
            {
                CreateRecord(domain, "CNAME", "example.com")
            });

            _netlifyServiceMock.Setup(s => s.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(records);

            _netlifyServiceMock.Setup(s => s.AddDnsRecordAsync(domain, domain, "A", ip, 1800, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecord(domain, "A", ip));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(domain, ip);

            // Assert
            Assert.IsTrue(result);
            _netlifyServiceMock.Verify(s => s.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            _netlifyServiceMock.Verify(s => s.AddDnsRecordAsync(domain, domain, "A", ip, 1800, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task UpdateDnsRecordAsync_IgnoresARecordsForDifferentHostname()
        {
            // Arrange
            string domain = "test.sakurapi.se";
            string ip = "1.2.3.4";

            // An A record exists but for a different hostname
            NetlifyDnsRecords records = new NetlifyDnsRecords(new List<NetlifyDnsRecord>
            {
                CreateRecord("other.sakurapi.se", "A", "9.9.9.9")
            });

            _netlifyServiceMock.Setup(s => s.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(records);

            _netlifyServiceMock.Setup(s => s.AddDnsRecordAsync(domain, domain, "A", ip, 1800, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecord(domain, "A", ip));

            // Act
            bool result = await _service.UpdateDnsRecordAsync(domain, ip);

            // Assert
            Assert.IsTrue(result);
            _netlifyServiceMock.Verify(s => s.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static NetlifyDnsRecords CreateRecords(string hostname, string type, string value)
        {
            return new NetlifyDnsRecords(new List<NetlifyDnsRecord> { CreateRecord(hostname, type, value) });
        }

        private static NetlifyDnsRecord CreateRecord(string hostname, string type, string value)
        {
            return new NetlifyDnsRecord(
                hostname: hostname,
                type: type,
                ttl: 1800,
                priority: null,
                weight: null,
                port: null,
                flag: null,
                tag: null,
                id: Guid.NewGuid().ToString(),
                siteId: null,
                dnsZoneId: "test_zone",
                errors: new List<object>(),
                managed: false,
                value: value);
        }
    }
}
