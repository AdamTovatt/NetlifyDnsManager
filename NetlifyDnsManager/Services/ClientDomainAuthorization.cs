using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// The single rule deciding which domains an authenticated client may change, used by every
    /// endpoint a client can reach. The claims it reads are the ones
    /// <see cref="ClientAuthValidationService"/> issues from the clients configuration.
    /// </summary>
    public static class ClientDomainAuthorization
    {
        /// <summary>
        /// The claim carrying one domain the client is allowed to change.
        /// </summary>
        public const string AllowedDomainClaim = "allowed_domain";

        /// <summary>
        /// Resolves the domain a request is for to the spelling the client's key authorizes.
        /// DNS names are case insensitive, so a request may name the domain in any case; everything
        /// downstream is given the configured spelling instead, which keeps record lookups and
        /// per-domain locking working off one form of the name.
        /// </summary>
        /// <param name="user">The authenticated client.</param>
        /// <param name="requestedDomain">The domain the request is for.</param>
        /// <param name="authorizedDomain">The configured spelling of that domain, when it is authorized.</param>
        /// <returns>True if the client's key authorizes that domain, false otherwise.</returns>
        public static bool TryGetAuthorizedDomain(ClaimsPrincipal user, string? requestedDomain, [NotNullWhen(true)] out string? authorizedDomain)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            authorizedDomain = null;

            if (string.IsNullOrWhiteSpace(requestedDomain))
                return false;

            authorizedDomain = user.Claims
                .Where(claim => claim.Type == AllowedDomainClaim)
                .Select(claim => claim.Value)
                .FirstOrDefault(allowedDomain => string.Equals(allowedDomain, requestedDomain, StringComparison.OrdinalIgnoreCase));

            return authorizedDomain != null;
        }
    }
}
