using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace NetlifyDnsManager.Endpoints
{
    /// <summary>
    /// How every endpoint a proxy client can reach behaves: which policy guards it, how a refused
    /// domain is reported, and what the client is told when the DNS provider fails.
    /// </summary>
    internal static class ClientRequest
    {
        /// <summary>
        /// The authorization policy for endpoints a client reaches with its API key.
        /// </summary>
        public const string ApiKeyPolicy = "ApiKeyOnly";

        /// <summary>
        /// The claim the client's name travels in, as issued by <see cref="Services.ClientAuthValidationService"/>.
        /// </summary>
        private const string SubjectClaim = "sub";

        /// <summary>
        /// Reports that the client's key does not authorize the domain it asked for.
        /// </summary>
        /// <param name="domain">The domain the request was for.</param>
        /// <returns>A 403 result.</returns>
        public static IResult NotAuthorizedForDomain(string domain)
        {
            return Results.Json(
                new { error = $"Not authorized to update domain: {domain}" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        /// <summary>
        /// Describes the authenticated client for the log, so that a write has an actor. The client's
        /// name travels as the token's subject, which is not what <see cref="System.Security.Principal.IIdentity.Name"/>
        /// reads, and which arrives mapped or unmapped depending on the token handler.
        /// </summary>
        /// <param name="httpContext">The HTTP context carrying the authenticated client.</param>
        /// <returns>The client's name, or a placeholder when the token carries none.</returns>
        public static string DescribeClient(HttpContext httpContext)
        {
            string?[] candidates = new string?[]
            {
                httpContext.User.Identity?.Name,
                httpContext.User.FindFirst(SubjectClaim)?.Value,
                httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            };

            return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? "(unnamed)";
        }

        /// <summary>
        /// Runs the work an endpoint does, turning a failure into a 500 for the client and a logged
        /// error for the operator. The client is told what failed but never how, because the reason
        /// comes from a third party and belongs in the log.
        /// </summary>
        /// <param name="logger">The endpoint's logger.</param>
        /// <param name="failureDescription">What the endpoint was doing, as a phrase for the message.</param>
        /// <param name="domain">The domain the request was for, for the log.</param>
        /// <param name="operation">The work to run.</param>
        /// <returns>The result of the work, or a 500 result.</returns>
        public static async Task<IResult> RunAsync(ILogger logger, string failureDescription, string domain, Func<Task<IResult>> operation)
        {
            try
            {
                return await operation();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to {FailureDescription} for domain {Domain}", failureDescription, domain);

                return Results.Json(
                    new { error = $"Failed to {failureDescription}." },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
