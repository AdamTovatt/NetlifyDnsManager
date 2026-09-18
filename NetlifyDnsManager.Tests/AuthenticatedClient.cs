using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NetlifyDnsManager.Services;
using System.Security.Claims;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Builds the HTTP context an endpoint sees for a client authenticated with an API key, carrying
    /// the same claims <see cref="ClientAuthValidationService"/> issues.
    /// </summary>
    internal static class AuthenticatedClient
    {
        /// <summary>
        /// The name the created client authenticates as, as the token's subject carries it.
        /// </summary>
        public const string Name = "Test Client";

        // Shared because writing a result only resolves logging from it, and a provider per call
        // would be built and dropped for every test
        private static readonly IServiceProvider ResultServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        /// <summary>
        /// Creates a context for a client authorized for the given domains.
        /// </summary>
        /// <param name="allowedDomains">The domains the client's key authorizes.</param>
        /// <returns>The HTTP context.</returns>
        public static HttpContext CreateContext(params string[] allowedDomains)
        {
            return CreateContext(allowedDomains, Enumerable.Empty<Claim>());
        }

        /// <summary>
        /// Creates a context for a client authorized for the given domains, carrying further claims.
        /// </summary>
        /// <param name="allowedDomains">The domains the client's key authorizes.</param>
        /// <param name="otherClaims">Further claims the token carries.</param>
        /// <returns>The HTTP context.</returns>
        public static HttpContext CreateContext(IEnumerable<string> allowedDomains, IEnumerable<Claim> otherClaims)
        {
            List<Claim> claims = allowedDomains
                .Select(domain => new Claim(ClientDomainAuthorization.AllowedDomainClaim, domain))
                .ToList();

            claims.Add(new Claim("sub", Name));
            claims.AddRange(otherClaims);

            ClaimsIdentity identity = new ClaimsIdentity(claims, authenticationType: "apikey");

            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                RequestServices = ResultServices,
                Response = { Body = new MemoryStream() }
            };
        }
    }
}
