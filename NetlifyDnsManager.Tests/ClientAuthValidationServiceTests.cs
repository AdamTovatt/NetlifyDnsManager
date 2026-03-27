using EasyReasy.Auth;
using Microsoft.Extensions.Logging;
using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for <see cref="ClientAuthValidationService"/> API key validation and JWT claim generation.
    /// </summary>
    [TestClass]
    public class ClientAuthValidationServiceTests
    {
        private const string JwtSecret = "this-is-a-test-secret-that-is-at-least-32-bytes-long";
        private ClientsConfiguration _clientsConfig = null!;
        private ClientAuthValidationService _service = null!;
        private IJwtTokenService _jwtTokenService = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _clientsConfig = new ClientsConfiguration
            {
                Clients = new List<ClientEntry>
                {
                    new ClientEntry
                    {
                        ApiKey = "valid-key-001",
                        AllowedDomains = new List<string> { "friend.sakurapi.se", "friend2.sakurapi.se" },
                        Name = "Friend"
                    },
                    new ClientEntry
                    {
                        ApiKey = "valid-key-002",
                        AllowedDomains = new List<string> { "other.sakurapi.se" },
                        Name = "Other"
                    }
                }
            };

            Mock<ILogger<ClientAuthValidationService>> loggerMock = new Mock<ILogger<ClientAuthValidationService>>();
            _service = new ClientAuthValidationService(_clientsConfig, loggerMock.Object);
            _jwtTokenService = new JwtTokenService(JwtSecret);
        }

        [TestMethod]
        public async Task ValidateApiKeyRequestAsync_WithValidKey_ReturnsAuthResponse()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("valid-key-001");

            // Act
            AuthResponse? response = await _service.ValidateApiKeyRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsFalse(string.IsNullOrEmpty(response.Token));
            Assert.IsFalse(string.IsNullOrEmpty(response.ExpiresAt));
        }

        [TestMethod]
        public async Task ValidateApiKeyRequestAsync_WithInvalidKey_ReturnsNull()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("unknown-key");

            // Act
            AuthResponse? response = await _service.ValidateApiKeyRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNull(response);
        }

        [TestMethod]
        public async Task ValidateApiKeyRequestAsync_JwtContainsAllowedDomainClaims()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("valid-key-001");

            // Act
            AuthResponse? response = await _service.ValidateApiKeyRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNotNull(response);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            List<string> allowedDomains = token.Claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .ToList();

            Assert.AreEqual(2, allowedDomains.Count);
            Assert.IsTrue(allowedDomains.Contains("friend.sakurapi.se"));
            Assert.IsTrue(allowedDomains.Contains("friend2.sakurapi.se"));
        }

        [TestMethod]
        public async Task ValidateApiKeyRequestAsync_SecondClient_HasCorrectClaims()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("valid-key-002");

            // Act
            AuthResponse? response = await _service.ValidateApiKeyRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNotNull(response);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            List<string> allowedDomains = token.Claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value)
                .ToList();

            Assert.AreEqual(1, allowedDomains.Count);
            Assert.IsTrue(allowedDomains.Contains("other.sakurapi.se"));
        }

        [TestMethod]
        public async Task ValidateApiKeyRequestAsync_JwtContainsApiKeyAuthType()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("valid-key-001");

            // Act
            AuthResponse? response = await _service.ValidateApiKeyRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNotNull(response);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            string? authType = token.Claims.FirstOrDefault(c => c.Type == "auth_type")?.Value;
            Assert.AreEqual("apikey", authType);
        }

        [TestMethod]
        public async Task ValidateApiKeyRequestAsync_JwtContainsClientNameAsSubject()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("valid-key-001");

            // Act
            AuthResponse? response = await _service.ValidateApiKeyRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNotNull(response);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            string? subject = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            Assert.AreEqual("Friend", subject);
        }

        [TestMethod]
        public async Task ValidateLoginRequestAsync_AlwaysReturnsNull()
        {
            // Arrange
            LoginAuthRequest request = new LoginAuthRequest("user", "pass");

            // Act
            AuthResponse? response = await _service.ValidateLoginRequestAsync(request, _jwtTokenService);

            // Assert
            Assert.IsNull(response);
        }
    }
}
