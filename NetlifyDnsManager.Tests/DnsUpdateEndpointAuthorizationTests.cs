using System.Security.Claims;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for domain authorization logic that validates allowed_domain claims
    /// against requested domains.
    /// </summary>
    [TestClass]
    public class DnsUpdateEndpointAuthorizationTests
    {
        [TestMethod]
        public void AllowedDomains_ContainsRequestedDomain_IsAuthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>
            {
                new Claim("allowed_domain", "friend.sakurapi.se"),
                new Claim("allowed_domain", "friend2.sakurapi.se")
            };

            string requestedDomain = "friend.sakurapi.se";

            // Act
            bool isAuthorized = claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .Contains(requestedDomain, StringComparer.OrdinalIgnoreCase);

            // Assert
            Assert.IsTrue(isAuthorized);
        }

        [TestMethod]
        public void AllowedDomains_DoesNotContainRequestedDomain_IsNotAuthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>
            {
                new Claim("allowed_domain", "friend.sakurapi.se")
            };

            string requestedDomain = "other.sakurapi.se";

            // Act
            bool isAuthorized = claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .Contains(requestedDomain, StringComparer.OrdinalIgnoreCase);

            // Assert
            Assert.IsFalse(isAuthorized);
        }

        [TestMethod]
        public void AllowedDomains_CaseInsensitiveMatch_IsAuthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>
            {
                new Claim("allowed_domain", "Friend.Sakurapi.Se")
            };

            string requestedDomain = "friend.sakurapi.se";

            // Act
            bool isAuthorized = claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .Contains(requestedDomain, StringComparer.OrdinalIgnoreCase);

            // Assert
            Assert.IsTrue(isAuthorized);
        }

        [TestMethod]
        public void AllowedDomains_EmptyClaims_IsNotAuthorized()
        {
            // Arrange
            List<Claim> claims = new List<Claim>();
            string requestedDomain = "friend.sakurapi.se";

            // Act
            bool isAuthorized = claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .Contains(requestedDomain, StringComparer.OrdinalIgnoreCase);

            // Assert
            Assert.IsFalse(isAuthorized);
        }

        [TestMethod]
        public void AllowedDomains_ClientCannotUpdateOtherClientsDomain()
        {
            // Arrange - Client A's claims
            List<Claim> clientAClaims = new List<Claim>
            {
                new Claim("allowed_domain", "clienta.sakurapi.se")
            };

            // Client A tries to update Client B's domain
            string requestedDomain = "clientb.sakurapi.se";

            // Act
            bool isAuthorized = clientAClaims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .Contains(requestedDomain, StringComparer.OrdinalIgnoreCase);

            // Assert
            Assert.IsFalse(isAuthorized);
        }

        [TestMethod]
        public void AllowedDomains_MultipleDomainsAllowed_CanUpdateAny()
        {
            // Arrange
            List<Claim> claims = new List<Claim>
            {
                new Claim("allowed_domain", "site1.sakurapi.se"),
                new Claim("allowed_domain", "site2.sakurapi.se"),
                new Claim("allowed_domain", "site3.sakurapi.se")
            };

            // Act & Assert - All three should be authorized
            foreach (string domain in new[] { "site1.sakurapi.se", "site2.sakurapi.se", "site3.sakurapi.se" })
            {
                bool isAuthorized = claims
                    .Where(c => c.Type == "allowed_domain")
                    .Select(c => c.Value)
                    .Contains(domain, StringComparer.OrdinalIgnoreCase);

                Assert.IsTrue(isAuthorized, $"Should be authorized for {domain}");
            }

            // But not for a fourth domain
            bool isUnauthorized = claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .Contains("site4.sakurapi.se", StringComparer.OrdinalIgnoreCase);

            Assert.IsFalse(isUnauthorized);
        }
    }
}
