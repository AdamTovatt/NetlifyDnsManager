using EasyReasy.Auth;
using Microsoft.AspNetCore.Http;
using NetlifyDnsManager.Models;
using System.Security.Claims;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Validates API key authentication requests against the clients configuration.
    /// Issues JWT tokens with allowed domain claims for authorized clients.
    /// </summary>
    public class ClientAuthValidationService : IAuthRequestValidationService
    {
        private readonly ClientsConfiguration _clientsConfiguration;
        private readonly ILogger<ClientAuthValidationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientAuthValidationService"/> class.
        /// </summary>
        /// <param name="clientsConfiguration">The clients configuration containing authorized API keys and their allowed domains.</param>
        /// <param name="logger">The logger instance.</param>
        public ClientAuthValidationService(ClientsConfiguration clientsConfiguration, ILogger<ClientAuthValidationService> logger)
        {
            _clientsConfiguration = clientsConfiguration ?? throw new ArgumentNullException(nameof(clientsConfiguration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates an API key request. If the key matches a configured client, returns a JWT
        /// with the client's allowed domains encoded as claims.
        /// </summary>
        /// <param name="request">The API key authentication request.</param>
        /// <param name="jwtTokenService">The JWT token service for creating tokens.</param>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>An AuthResponse with a JWT if the key is valid, null otherwise.</returns>
        public Task<AuthResponse?> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
        {
            ClientEntry? client = _clientsConfiguration.FindByApiKey(request.ApiKey);

            if (client == null)
            {
                _logger.LogWarning("Authentication failed: unknown API key");
                return Task.FromResult<AuthResponse?>(null);
            }

            string subject = client.Name ?? "client";
            List<Claim> claims = new List<Claim>();

            foreach (string domain in client.AllowedDomains)
            {
                claims.Add(new Claim("allowed_domain", domain));
            }

            DateTime expiresAt = DateTime.UtcNow.AddHours(1);
            string token = jwtTokenService.CreateToken(
                subject: subject,
                authType: "apikey",
                additionalClaims: claims,
                roles: Enumerable.Empty<string>(),
                expiresAt: expiresAt);

            AuthResponse response = new AuthResponse(token, expiresAt.ToString("O"));

            _logger.LogInformation("Client '{ClientName}' authenticated successfully", subject);

            return Task.FromResult<AuthResponse?>(response);
        }

        /// <summary>
        /// Username/password authentication is not supported for DNS proxy clients.
        /// </summary>
        public Task<AuthResponse?> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
        {
            return Task.FromResult<AuthResponse?>(null);
        }
    }
}
