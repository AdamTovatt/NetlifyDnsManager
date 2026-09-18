using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NetlifyDnsManager.Services;
using System.Security.Claims;

namespace NetlifyDnsManager.Tests.TestSupport
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
            return CreateContextWithExactClaims(
                allowedDomains,
                identityName: null,
                otherClaims.Append(new Claim("sub", Name)));
        }

        /// <summary>
        /// Creates a context whose token carries exactly the claims given, with no subject added, and
        /// whose identity has a name only when one is passed. This is how a token that arrives mapped
        /// differently is reproduced: the client's name is then not where the subject claim is.
        /// </summary>
        /// <param name="allowedDomains">The domains the client's key authorizes.</param>
        /// <param name="identityName">The name the identity reports, or null for an identity with none.</param>
        /// <param name="claims">The claims the token carries, beyond the allowed domains.</param>
        /// <returns>The HTTP context.</returns>
        public static HttpContext CreateContextWithExactClaims(IEnumerable<string> allowedDomains, string? identityName, IEnumerable<Claim> claims)
        {
            List<Claim> allClaims = allowedDomains
                .Select(domain => new Claim(ClientDomainAuthorization.AllowedDomainClaim, domain))
                .ToList();

            allClaims.AddRange(claims);

            if (identityName != null)
            {
                allClaims.Add(new Claim(ClaimTypes.Name, identityName));
            }

            ClaimsIdentity identity = new ClaimsIdentity(
                allClaims,
                authenticationType: "apikey",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                RequestServices = ResultServices,
                Response = { Body = new MemoryStream() }
            };
        }
    }
}
