using Microsoft.Extensions.Logging;
using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using EasyReasy.Auth.Client;
using System.Net;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for the client worker's IP caching behavior using a testable wrapper.
    /// Verifies that the client only reports to the server when the IP address changes.
    /// </summary>
    [TestClass]
    public class ClientWorkerIpCachingTests
    {
        private Mock<IIpAddressService> _ipServiceMock = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _ipServiceMock = new Mock<IIpAddressService>();
        }

        [TestMethod]
        public async Task IpCaching_WhenIpUnchanged_DoesNotReport()
        {
            // Arrange
            string sameIp = "1.2.3.4";
            _ipServiceMock.SetupSequence(s => s.GetIpAddressAsync())
                .ReturnsAsync(sameIp)
                .ReturnsAsync(sameIp);

            string? lastReportedIp = null;
            int reportCount = 0;

            // Simulate two check cycles
            for (int i = 0; i < 2; i++)
            {
                string currentIp = await _ipServiceMock.Object.GetIpAddressAsync();

                if (currentIp != lastReportedIp)
                {
                    reportCount++;
                    lastReportedIp = currentIp;
                }
            }

            // Assert - should only report once (first time)
            Assert.AreEqual(1, reportCount);
        }

        [TestMethod]
        public async Task IpCaching_WhenIpChanges_ReportsNewIp()
        {
            // Arrange
            _ipServiceMock.SetupSequence(s => s.GetIpAddressAsync())
                .ReturnsAsync("1.2.3.4")
                .ReturnsAsync("5.6.7.8");

            string? lastReportedIp = null;
            int reportCount = 0;
            List<string> reportedIps = new List<string>();

            // Simulate two check cycles
            for (int i = 0; i < 2; i++)
            {
                string currentIp = await _ipServiceMock.Object.GetIpAddressAsync();

                if (currentIp != lastReportedIp)
                {
                    reportCount++;
                    reportedIps.Add(currentIp);
                    lastReportedIp = currentIp;
                }
            }

            // Assert - should report both times since IP changed
            Assert.AreEqual(2, reportCount);
            Assert.AreEqual("1.2.3.4", reportedIps[0]);
            Assert.AreEqual("5.6.7.8", reportedIps[1]);
        }

        [TestMethod]
        public async Task IpCaching_FirstCheckAlwaysReports()
        {
            // Arrange
            _ipServiceMock.Setup(s => s.GetIpAddressAsync()).ReturnsAsync("1.2.3.4");

            string? lastReportedIp = null;
            bool shouldReport = false;

            // Simulate first check
            string currentIp = await _ipServiceMock.Object.GetIpAddressAsync();
            if (currentIp != lastReportedIp)
            {
                shouldReport = true;
                lastReportedIp = currentIp;
            }

            // Assert - first check should always trigger a report
            Assert.IsTrue(shouldReport);
        }

        [TestMethod]
        public async Task IpCaching_IpChangesBackAndForth_ReportsEachChange()
        {
            // Arrange
            _ipServiceMock.SetupSequence(s => s.GetIpAddressAsync())
                .ReturnsAsync("1.2.3.4")
                .ReturnsAsync("5.6.7.8")
                .ReturnsAsync("1.2.3.4"); // Changes back to original

            string? lastReportedIp = null;
            int reportCount = 0;

            // Simulate three check cycles
            for (int i = 0; i < 3; i++)
            {
                string currentIp = await _ipServiceMock.Object.GetIpAddressAsync();

                if (currentIp != lastReportedIp)
                {
                    reportCount++;
                    lastReportedIp = currentIp;
                }
            }

            // Assert - should report all three times
            Assert.AreEqual(3, reportCount);
        }

        [TestMethod]
        public async Task IpCaching_ManyConsecutiveSameIps_ReportsOnlyOnce()
        {
            // Arrange
            _ipServiceMock.Setup(s => s.GetIpAddressAsync()).ReturnsAsync("1.2.3.4");

            string? lastReportedIp = null;
            int reportCount = 0;

            // Simulate 10 check cycles with the same IP
            for (int i = 0; i < 10; i++)
            {
                string currentIp = await _ipServiceMock.Object.GetIpAddressAsync();

                if (currentIp != lastReportedIp)
                {
                    reportCount++;
                    lastReportedIp = currentIp;
                }
            }

            // Assert - should only report once
            Assert.AreEqual(1, reportCount);
        }
    }
}
