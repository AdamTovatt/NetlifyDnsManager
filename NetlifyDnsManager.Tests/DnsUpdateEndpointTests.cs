using Microsoft.AspNetCore.Http;
using Moq;
using NetlifyDnsManager.Endpoints;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the update endpoint: which requests reach the DNS layer, with which domain, and what the
    /// client is told about the outcome.
    /// </summary>
    [TestClass]
    public class DnsUpdateEndpointTests
    {
        private const string AuthorizedDomain = "mine.example.com";
        private const string OtherClientsDomain = "theirs.example.com";
        private const string IpAddress = "10.1.1.7";

        private Mock<IDnsUpdateService> _dnsUpdateServiceMock = null!;
        private CapturingLoggerFactory _loggerFactory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _dnsUpdateServiceMock = new Mock<IDnsUpdateService>();
            _loggerFactory = new CapturingLoggerFactory();
        }

        [TestMethod]
        public async Task DnsUpdate_ForAnAuthorizedDomain_UpdatesTheRecordAndReportsIt()
        {
            // Arrange
            SetUpUpdate(AuthorizedDomain, updated: true);

            // Act
            EndpointResponse response = await ExecuteUpdateAsync(AuthorizedDomain, IpAddress);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(AuthorizedDomain, response.Field("domain"));
            Assert.AreEqual(IpAddress, response.Field("ip"));
            Assert.AreEqual("true", response.Field("updated"));

            _dnsUpdateServiceMock.Verify(
                service => service.UpdateDnsRecordAsync(AuthorizedDomain, IpAddress, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DnsUpdate_WhenTheRecordWasAlreadyCurrent_ReportsThatNothingChanged()
        {
            // Arrange
            SetUpUpdate(AuthorizedDomain, updated: false);

            // Act
            EndpointResponse response = await ExecuteUpdateAsync(AuthorizedDomain, IpAddress);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual("false", response.Field("updated"));
        }

        [TestMethod]
        public async Task DnsUpdate_WithTheDomainInAnotherCase_UsesTheAuthorizedSpelling()
        {
            // Arrange - the record lookup and the per-domain lock should see one form of the name
            _dnsUpdateServiceMock
                .Setup(service => service.UpdateDnsRecordAsync(It.IsAny<string>(), IpAddress, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            EndpointResponse response = await ExecuteUpdateAsync("MINE.Example.COM", IpAddress);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(AuthorizedDomain, response.Field("domain"));

            _dnsUpdateServiceMock.Verify(
                service => service.UpdateDnsRecordAsync(AuthorizedDomain, IpAddress, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DnsUpdate_ForAnotherClientsDomain_IsForbiddenAndUpdatesNothing()
        {
            // Act - the client is authorized for AuthorizedDomain only
            EndpointResponse response = await ExecuteUpdateAsync(OtherClientsDomain, IpAddress);

            // Assert
            Assert.AreEqual(StatusCodes.Status403Forbidden, response.StatusCode);
            Assert.AreEqual($"Not authorized for domain: {OtherClientsDomain}", response.Field("error"));
            VerifyNothingUpdated();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DnsUpdate_WithoutADomain_IsRejectedAndUpdatesNothing(string? domain)
        {
            EndpointResponse response = await ExecuteUpdateAsync(domain, IpAddress);

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingUpdated();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DnsUpdate_WithoutAnIpAddress_IsRejectedAndUpdatesNothing(string? ipAddress)
        {
            EndpointResponse response = await ExecuteUpdateAsync(AuthorizedDomain, ipAddress);

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingUpdated();
        }

        [TestMethod]
        public async Task DnsUpdate_WithAnInvalidIpAddress_IsRejectedAndUpdatesNothing()
        {
            EndpointResponse response = await ExecuteUpdateAsync(AuthorizedDomain, "not-an-ip");

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingUpdated();
        }

        [TestMethod]
        public async Task DnsUpdate_WhenTheDnsLayerFails_ReportsAServerErrorWithoutTheReason()
        {
            // Arrange
            _dnsUpdateServiceMock
                .Setup(service => service.UpdateDnsRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)"));

            // Act
            EndpointResponse response = await ExecuteUpdateAsync(AuthorizedDomain, IpAddress);

            // Assert
            Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
            Assert.IsFalse(response.Body.Contains("401"), $"The provider's reason reached the client: {response.Body}");
        }

        [TestMethod]
        public async Task DnsUpdate_PassesTheRequestsCancellationTokenOn()
        {
            // Arrange - the client that reported an address may hang up while the zone is being written
            using CancellationTokenSource requestAborted = new CancellationTokenSource();

            _dnsUpdateServiceMock
                .Setup(service => service.UpdateDnsRecordAsync(AuthorizedDomain, IpAddress, It.IsAny<bool>(), requestAborted.Token))
                .ReturnsAsync(true);

            // Act
            EndpointResponse response = await ExecuteUpdateAsync(AuthorizedDomain, IpAddress, requestAborted.Token);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            _dnsUpdateServiceMock.Verify(
                service => service.UpdateDnsRecordAsync(AuthorizedDomain, IpAddress, It.IsAny<bool>(), requestAborted.Token),
                Times.Once);
        }

        [TestMethod]
        public async Task DnsUpdate_LogsTheClientThatReported()
        {
            // Arrange
            SetUpUpdate(AuthorizedDomain, updated: true);

            // Act
            await ExecuteUpdateAsync(AuthorizedDomain, IpAddress);

            // Assert
            Assert.IsTrue(
                _loggerFactory.Messages.Any(message => message.Contains(AuthenticatedClient.Name) && message.Contains(AuthorizedDomain)),
                $"Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task DnsUpdate_LogsUnderTheEndpointsFullTypeName()
        {
            // Arrange
            SetUpUpdate(AuthorizedDomain, updated: true);

            // Act
            await ExecuteUpdateAsync(AuthorizedDomain, IpAddress);

            // Assert
            CollectionAssert.Contains(_loggerFactory.Categories.ToList(), typeof(DnsUpdateEndpoints).FullName);
        }

        private void SetUpUpdate(string domain, bool updated)
        {
            _dnsUpdateServiceMock
                .Setup(service => service.UpdateDnsRecordAsync(domain, IpAddress, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updated);
        }

        private async Task<EndpointResponse> ExecuteUpdateAsync(string? requestedDomain, string? ipAddress, CancellationToken cancellationToken = default)
        {
            DnsUpdateRequest request = new DnsUpdateRequest
            {
                Domain = requestedDomain!,
                Ip = ipAddress!
            };

            HttpContext httpContext = AuthenticatedClient.CreateContext(AuthorizedDomain);

            IResult result = await DnsUpdateEndpoints.HandleDnsUpdateAsync(
                request,
                _dnsUpdateServiceMock.Object,
                httpContext,
                _loggerFactory,
                cancellationToken);

            return await EndpointResponse.ReadAsync(result, httpContext);
        }

        private void VerifyNothingUpdated()
        {
            _dnsUpdateServiceMock.Verify(
                service => service.UpdateDnsRecordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
