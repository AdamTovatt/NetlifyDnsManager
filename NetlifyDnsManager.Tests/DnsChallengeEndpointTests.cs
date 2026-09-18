using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NetlifyDnsManager.Endpoints;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using System.Security.Claims;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the challenge endpoints themselves: which requests reach the DNS layer at all, what the
    /// client is told, and that a client can only ever affect the challenge name under a domain its
    /// key authorizes.
    /// </summary>
    [TestClass]
    public class DnsChallengeEndpointTests
    {
        private const string AuthorizedDomain = "mine.example.com";
        private const string AuthorizedChallengeName = "_acme-challenge.mine.example.com";
        private const string OtherClientsDomain = "theirs.example.com";
        private const string ChallengeValue = "challenge-value-as-published-by-the-acme-client";

        private Mock<IDnsChallengeService> _challengeServiceMock = null!;
        private CapturingLoggerFactory _loggerFactory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _challengeServiceMock = new Mock<IDnsChallengeService>();
            _loggerFactory = new CapturingLoggerFactory();
        }

        [TestMethod]
        public async Task SetChallenge_ForAnAuthorizedDomain_PublishesTheValueAndReportsIt()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.Created);

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert - the client is told where the value went, and that it was written
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(AuthorizedDomain, response.Field("domain"));
            Assert.AreEqual(AuthorizedChallengeName, response.Field("recordName"));
            Assert.AreEqual("true", response.Field("created"));

            _challengeServiceMock.Verify(
                service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheValueWasAlreadyPublished_ReportsThatNothingWasCreated()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.AlreadyPublished);

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert - a client repeating a publish must be able to tell the two outcomes apart
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual("false", response.Field("created"));
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheRecordHoldsTooManyValues_IsRefusedAsAConflict()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.TooManyValues);

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert - not a 500: the zone needs cleaning up, and the message says so
            Assert.AreEqual(StatusCodes.Status409Conflict, response.StatusCode);
            StringAssert.Contains(response.Field("error")!, AuthorizedChallengeName);
        }

        [TestMethod]
        public async Task SetChallenge_WithTheDomainInAnotherCase_UsesTheAuthorizedSpelling()
        {
            // Arrange - DNS names are case insensitive, but everything downstream should see one form
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(It.IsAny<string>(), ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.Created);

            // Act
            EndpointResponse response = await ExecuteSetAsync("MINE.Example.COM", ChallengeValue);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(AuthorizedDomain, response.Field("domain"));
            Assert.AreEqual(AuthorizedChallengeName, response.Field("recordName"));

            _challengeServiceMock.Verify(
                service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task SetChallenge_ForAnotherClientsDomain_IsForbiddenAndWritesNothing()
        {
            // Act
            EndpointResponse response = await ExecuteSetAsync(OtherClientsDomain, ChallengeValue);

            // Assert
            Assert.AreEqual(StatusCodes.Status403Forbidden, response.StatusCode);
            VerifyNothingPublished();
        }

        [TestMethod]
        [DataRow("_acme-challenge.theirs.example.com")]
        [DataRow("theirs.example.com.mine.example.com")]
        [DataRow("mine.example.com.attacker.test")]
        [DataRow("*.example.com")]
        [DataRow("example.com")]
        public async Task SetChallenge_ForANameOutsideTheGrant_IsForbiddenAndWritesNothing(string domain)
        {
            // A client that could name the record itself could publish TXT records anywhere in the
            // zone, so the only accepted name is a domain its key lists, and the record name is derived
            EndpointResponse response = await ExecuteSetAsync(domain, ChallengeValue);

            Assert.AreEqual(StatusCodes.Status403Forbidden, response.StatusCode);
            VerifyNothingPublished();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task SetChallenge_WithoutADomain_IsRejectedAndWritesNothing(string? domain)
        {
            EndpointResponse response = await ExecuteSetAsync(domain, ChallengeValue);

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingPublished();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task SetChallenge_WithoutAValue_IsRejectedAndWritesNothing(string? value)
        {
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, value);

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingPublished();
        }

        [TestMethod]
        public async Task SetChallenge_WithAValueOfExactlyTheMaximumLength_IsAccepted()
        {
            // Arrange - the boundary belongs to the accepted side
            string longestAllowedValue = new string('a', AcmeChallenge.MaxValueBytes);

            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, longestAllowedValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.Created);

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, longestAllowedValue);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        }

        [TestMethod]
        public async Task SetChallenge_WithAValueOneByteTooLong_IsRejectedAndWritesNothing()
        {
            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, new string('a', AcmeChallenge.MaxValueBytes + 1));

            // Assert
            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingPublished();
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheDnsLayerFails_ReportsAServerErrorWithoutTheReason()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)"));

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert - the client must not read a failed publish as a success, nor learn why it failed
            Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
            Assert.IsFalse(response.Body.Contains("401"), $"The provider's reason reached the client: {response.Body}");
            Assert.IsTrue(_loggerFactory.Messages.Any(message => message.Contains("publish the challenge record")),
                $"The failure was not logged. Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_LogsTheClientThatPublished()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.Created);

            // Act
            await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert - a write into a shared zone needs an actor in the log
            Assert.IsTrue(
                _loggerFactory.Messages.Any(message => message.Contains(AuthenticatedClient.Name) && message.Contains(AuthorizedChallengeName)),
                $"Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_LogsUnderTheEndpointsFullTypeName()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.Created);

            // Act
            await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert - log filters are configured by namespace prefix, which a short name escapes
            CollectionAssert.Contains(_loggerFactory.Categories.ToList(), typeof(DnsChallengeEndpoints).FullName);
        }

        [TestMethod]
        public async Task DeleteChallenge_ForAnAuthorizedDomain_RemovesTheRecordsAndReportsHowMany()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            EndpointResponse response = await ExecuteDeleteAsync(AuthorizedDomain, value: null);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(AuthorizedChallengeName, response.Field("recordName"));
            Assert.AreEqual("2", response.Field("deleted"));

            _challengeServiceMock.Verify(
                service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DeleteChallenge_WithAValue_PassesThatValueOn()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            EndpointResponse response = await ExecuteDeleteAsync(AuthorizedDomain, ChallengeValue);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual("1", response.Field("deleted"));

            _challengeServiceMock.Verify(
                service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DeleteChallenge_WithTheDomainInAnotherCase_UsesTheAuthorizedSpelling()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.DeleteChallengeRecordsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            EndpointResponse response = await ExecuteDeleteAsync("MINE.Example.COM", value: null);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            Assert.AreEqual(AuthorizedDomain, response.Field("domain"));

            _challengeServiceMock.Verify(
                service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task DeleteChallenge_ForAnotherClientsDomain_IsForbiddenAndRemovesNothing()
        {
            // Act
            EndpointResponse response = await ExecuteDeleteAsync(OtherClientsDomain, value: null);

            // Assert
            Assert.AreEqual(StatusCodes.Status403Forbidden, response.StatusCode);
            VerifyNothingRemoved();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DeleteChallenge_WithoutADomain_IsRejectedAndRemovesNothing(string? domain)
        {
            EndpointResponse response = await ExecuteDeleteAsync(domain, value: null);

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingRemoved();
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DeleteChallenge_WithABlankValue_IsRejectedAndRemovesNothing(string value)
        {
            // A cleanup hook whose value variable is unset sends "?value=", which arrives as an empty
            // string rather than as no value at all. Removing nothing and answering 200 would tell that
            // hook the challenge record was cleaned up when it is still published
            EndpointResponse response = await ExecuteDeleteAsync(AuthorizedDomain, value);

            Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
            VerifyNothingRemoved();
        }

        [TestMethod]
        public async Task DeleteChallenge_ForAnotherClientsDomain_SaysWhatItIsNotAuthorizedFor()
        {
            // The one refusal both endpoints answer with, so it names no operation
            EndpointResponse response = await ExecuteDeleteAsync(OtherClientsDomain, value: null);

            Assert.AreEqual($"Not authorized for domain: {OtherClientsDomain}", response.Field("error"));
        }

        [TestMethod]
        public async Task DeleteChallenge_WhenTheDnsLayerFails_ReportsAServerErrorWithoutTheReason()
        {
            // Arrange - a client that read this as a success would leave the challenge record behind
            _challengeServiceMock.Setup(service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized)"));

            // Act
            EndpointResponse response = await ExecuteDeleteAsync(AuthorizedDomain, value: null);

            // Assert
            Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
            Assert.IsFalse(response.Body.Contains("401"), $"The provider's reason reached the client: {response.Body}");
            Assert.IsTrue(_loggerFactory.Messages.Any(message => message.Contains("remove the challenge records")),
                $"The failure was not logged. Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task DeleteChallenge_LogsTheClientThatRemovedTheRecords()
        {
            // Arrange
            _challengeServiceMock.Setup(service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await ExecuteDeleteAsync(AuthorizedDomain, value: null);

            // Assert
            Assert.IsTrue(
                _loggerFactory.Messages.Any(message => message.Contains(AuthenticatedClient.Name) && message.Contains(AuthorizedChallengeName)),
                $"Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_PassesTheRequestsCancellationTokenOn()
        {
            // Arrange - a client that went away should not leave a publish running
            using CancellationTokenSource requestAborted = new CancellationTokenSource();

            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, requestAborted.Token))
                .ReturnsAsync(ChallengeSetResult.Created);

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue, requestAborted.Token);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            _challengeServiceMock.Verify(
                service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, requestAborted.Token),
                Times.Once);
        }

        [TestMethod]
        public async Task DeleteChallenge_PassesTheRequestsCancellationTokenOn()
        {
            // Arrange
            using CancellationTokenSource requestAborted = new CancellationTokenSource();

            _challengeServiceMock.Setup(service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, requestAborted.Token))
                .ReturnsAsync(1);

            // Act
            EndpointResponse response = await ExecuteDeleteAsync(AuthorizedDomain, value: null, requestAborted.Token);

            // Assert
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            _challengeServiceMock.Verify(
                service => service.DeleteChallengeRecordsAsync(AuthorizedDomain, null, requestAborted.Token),
                Times.Once);
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheClientGoesAwayMidRequest_IsNotReportedAsAFailure()
        {
            // Arrange - the client hung up, so the request's own token is what ended the work
            using CancellationTokenSource requestAborted = new CancellationTokenSource();

            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException(requestAborted.Token));

            requestAborted.Cancel();

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue, requestAborted.Token);

            // Assert - an operator running with logging turned down sees errors only, and a client
            // hanging up is not something they can act on
            Assert.AreEqual(499, response.StatusCode);
            Assert.AreEqual(
                0,
                _loggerFactory.MessagesAt(LogLevel.Error).Count,
                $"A disconnect was logged as an error: {string.Join(" | ", _loggerFactory.MessagesAt(LogLevel.Error))}");
            Assert.IsTrue(
                _loggerFactory.MessagesAt(LogLevel.Information).Any(message => message.Contains(AuthorizedDomain)),
                $"The disconnect was not recorded at all. Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheCancellationIsNotTheRequestsOwn_IsStillAFailure()
        {
            // Arrange - a library may cancel on a token of its own, for a timeout of its own. The
            // request was never cancelled, so this is a failure and has to be reported as one
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("The provider gave up waiting."));

            // Act
            EndpointResponse response = await ExecuteSetAsync(AuthorizedDomain, ChallengeValue);

            // Assert
            Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
            Assert.IsTrue(
                _loggerFactory.MessagesAt(LogLevel.Error).Any(message => message.Contains("publish the challenge record")),
                $"The failure was not logged as one. Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheIdentityCarriesAName_LogsThatNameAsTheActor()
        {
            // Arrange - the same token, mapped so that the name is on the identity as well as in a
            // claim. The two names differ, so only reading the one that wins passes
            HttpContext httpContext = AuthenticatedClient.CreateContextWithExactClaims(
                new[] { AuthorizedDomain },
                identityName: "the-mapped-name",
                new[] { new Claim("sub", "the-subject-name") });

            SetUpSuccessfulPublish();

            // Act
            await ExecuteSetAsync(httpContext, AuthorizedDomain, ChallengeValue);

            // Assert
            Assert.IsTrue(
                _loggerFactory.Messages.Any(message => message.Contains("the-mapped-name")),
                $"Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheNameIsOnlyInTheNameIdentifierClaim_LogsThatName()
        {
            // Arrange - a handler that maps the subject to the standard claim type instead
            HttpContext httpContext = AuthenticatedClient.CreateContextWithExactClaims(
                new[] { AuthorizedDomain },
                identityName: null,
                new[] { new Claim(ClaimTypes.NameIdentifier, "the-name-identifier") });

            SetUpSuccessfulPublish();

            // Act
            await ExecuteSetAsync(httpContext, AuthorizedDomain, ChallengeValue);

            // Assert
            Assert.IsTrue(
                _loggerFactory.Messages.Any(message => message.Contains("the-name-identifier")),
                $"Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        [TestMethod]
        public async Task SetChallenge_WhenTheTokenCarriesNoNameAtAll_SaysSoAndStillLogsTheWrite()
        {
            // Arrange - a token with no name anywhere must not cost the log its entry
            HttpContext httpContext = AuthenticatedClient.CreateContextWithExactClaims(
                new[] { AuthorizedDomain },
                identityName: null,
                Enumerable.Empty<Claim>());

            SetUpSuccessfulPublish();

            // Act
            await ExecuteSetAsync(httpContext, AuthorizedDomain, ChallengeValue);

            // Assert - the entry says the actor is unknown rather than leaving a blank where it goes
            Assert.IsTrue(
                _loggerFactory.Messages.Any(message => message.Contains("(unnamed)") && message.Contains(AuthorizedChallengeName)),
                $"Logged: {string.Join(" | ", _loggerFactory.Messages)}");
        }

        private void SetUpSuccessfulPublish()
        {
            _challengeServiceMock.Setup(service => service.SetChallengeRecordAsync(AuthorizedDomain, ChallengeValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChallengeSetResult.Created);
        }

        private Task<EndpointResponse> ExecuteSetAsync(string? domain, string? value, CancellationToken cancellationToken = default)
        {
            return ExecuteSetAsync(AuthenticatedClient.CreateContext(AuthorizedDomain), domain, value, cancellationToken);
        }

        private async Task<EndpointResponse> ExecuteSetAsync(HttpContext httpContext, string? domain, string? value, CancellationToken cancellationToken = default)
        {
            DnsChallengeRequest request = new DnsChallengeRequest
            {
                Domain = domain!,
                Value = value!
            };

            IResult result = await DnsChallengeEndpoints.HandleSetChallengeAsync(
                request,
                _challengeServiceMock.Object,
                httpContext,
                _loggerFactory,
                cancellationToken);

            return await EndpointResponse.ReadAsync(result, httpContext);
        }

        private async Task<EndpointResponse> ExecuteDeleteAsync(string? domain, string? value, CancellationToken cancellationToken = default)
        {
            HttpContext httpContext = AuthenticatedClient.CreateContext(AuthorizedDomain);

            IResult result = await DnsChallengeEndpoints.HandleDeleteChallengeAsync(
                domain,
                value,
                _challengeServiceMock.Object,
                httpContext,
                _loggerFactory,
                cancellationToken);

            return await EndpointResponse.ReadAsync(result, httpContext);
        }

        private void VerifyNothingPublished()
        {
            _challengeServiceMock.Verify(
                service => service.SetChallengeRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private void VerifyNothingRemoved()
        {
            _challengeServiceMock.Verify(
                service => service.DeleteChallengeRecordsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
