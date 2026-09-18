using Microsoft.AspNetCore.Http;
using NetlifyDnsManager.Services;
using System.Security.Claims;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the one rule deciding which domains a client may change, which every endpoint a client
    /// can reach goes through.
    /// </summary>
    [TestClass]
    public class ClientDomainAuthorizationTests
    {
        [TestMethod]
        public void TryGetAuthorizedDomain_WhenTheDomainIsListed_IsAuthorized()
        {
            HttpContext httpContext = AuthenticatedClient.CreateContext("friend.sakurapi.se", "friend2.sakurapi.se");

            Assert.IsTrue(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "friend2.sakurapi.se", out string? domain));
            Assert.AreEqual("friend2.sakurapi.se", domain);
        }

        [TestMethod]
        public void TryGetAuthorizedDomain_WhenTheDomainIsNotListed_IsNotAuthorized()
        {
            HttpContext httpContext = AuthenticatedClient.CreateContext("friend.sakurapi.se");

            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "other.sakurapi.se", out string? domain));
            Assert.IsNull(domain);
        }

        [TestMethod]
        public void TryGetAuthorizedDomain_WhenTheCaseDiffers_ReturnsTheConfiguredSpelling()
        {
            // Arrange - the allow list is what the zone's records are named after
            HttpContext httpContext = AuthenticatedClient.CreateContext("Friend.Sakurapi.Se");

            // Act
            bool authorized = ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "friend.sakurapi.se", out string? domain);

            // Assert - the request is accepted, and what travels on is the configured form
            Assert.IsTrue(authorized);
            Assert.AreEqual("Friend.Sakurapi.Se", domain);
        }

        [TestMethod]
        public void TryGetAuthorizedDomain_WithoutAnyClaims_IsNotAuthorized()
        {
            HttpContext httpContext = AuthenticatedClient.CreateContext();

            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "friend.sakurapi.se", out _));
        }

        [TestMethod]
        public void TryGetAuthorizedDomain_DoesNotExtendToSubdomainsOrParents()
        {
            HttpContext httpContext = AuthenticatedClient.CreateContext("friend.sakurapi.se");

            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "sakurapi.se", out _));
            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "sub.friend.sakurapi.se", out _));
            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "friend.sakurapi.se.attacker.test", out _));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void TryGetAuthorizedDomain_WithoutADomain_IsNotAuthorized(string? domain)
        {
            HttpContext httpContext = AuthenticatedClient.CreateContext("friend.sakurapi.se");

            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, domain, out _));
        }

        [TestMethod]
        public void TryGetAuthorizedDomain_IgnoresOtherClaimTypes()
        {
            // Arrange - a claim of another type must not grant a domain
            HttpContext httpContext = AuthenticatedClient.CreateContext(
                Enumerable.Empty<string>(),
                new[] { new Claim("some_other_claim", "friend.sakurapi.se") });

            // Assert
            Assert.IsFalse(ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, "friend.sakurapi.se", out _));
        }

        [TestMethod]
        public void TryGetAuthorizedDomain_WithoutAUser_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => ClientDomainAuthorization.TryGetAuthorizedDomain(null!, "friend.sakurapi.se", out _));
        }
    }
}
