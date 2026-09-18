using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Endpoints
{
    /// <summary>
    /// Extension methods for mapping the ACME DNS-01 challenge endpoints for proxy clients.
    /// </summary>
    public static class DnsChallengeEndpoints
    {
        /// <summary>
        /// Maps the challenge record endpoints for proxy clients.
        /// Requires API key authentication, and authorizes the domain the same way the update endpoint does.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication MapDnsChallengeEndpoints(this WebApplication app)
        {
            // Both verbs on one route, written out rather than grouped: a route group would publish
            // the path with a trailing slash, which is a change to a documented URL
            const string challengeRoute = "/api/dns/challenge";

            app.MapPost(challengeRoute, HandleSetChallengeAsync)
                .RequireAuthorization(ClientRequest.ApiKeyPolicy);

            app.MapDelete(challengeRoute, HandleDeleteChallengeAsync)
                .RequireAuthorization(ClientRequest.ApiKeyPolicy);

            return app;
        }

        /// <summary>
        /// Publishes a challenge value for a domain the authenticated client is authorized for.
        /// </summary>
        /// <param name="request">The challenge request.</param>
        /// <param name="dnsChallengeService">The service managing challenge records.</param>
        /// <param name="httpContext">The HTTP context carrying the authenticated client.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="cancellationToken">Cancelled when the client goes away.</param>
        /// <returns>The result of the request.</returns>
        public static async Task<IResult> HandleSetChallengeAsync(
            DnsChallengeRequest request,
            IDnsChallengeService dnsChallengeService,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Domain))
                return Results.BadRequest(new { error = "Domain is required." });

            if (string.IsNullOrWhiteSpace(request.Value))
                return Results.BadRequest(new { error = "Challenge value is required." });

            if (AcmeChallenge.IsValueTooLong(request.Value))
                return Results.BadRequest(new { error = $"Challenge value cannot be longer than {AcmeChallenge.MaxValueBytes} bytes." });

            if (!ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, request.Domain, out string? domain))
                return ClientRequest.NotAuthorizedForDomain(request.Domain);

            ILogger logger = loggerFactory.CreateLogger(typeof(DnsChallengeEndpoints));

            return await ClientRequest.RunAsync(logger, "publish the challenge record", domain, cancellationToken, async () =>
            {
                ChallengeSetResult result = await dnsChallengeService.SetChallengeRecordAsync(domain, request.Value, cancellationToken);
                string recordName = AcmeChallenge.RecordNameFor(domain);

                if (result == ChallengeSetResult.TooManyValues)
                {
                    return Results.Json(
                        new
                        {
                            error = $"Record {recordName} already holds {AcmeChallenge.MaxValuesPerName} challenge values. " +
                                "Remove the ones left over from earlier challenges first.",
                            recordName
                        },
                        statusCode: StatusCodes.Status409Conflict);
                }

                logger.LogInformation(
                    "Client {ClientName} published a challenge value at {RecordName}",
                    ClientRequest.DescribeClient(httpContext),
                    recordName);

                return Results.Ok(new
                {
                    domain,
                    recordName,
                    created = result == ChallengeSetResult.Created
                });
            });
        }

        /// <summary>
        /// Removes challenge records for a domain the authenticated client is authorized for.
        /// </summary>
        /// <param name="domain">The domain being validated.</param>
        /// <param name="value">The single value to remove, or null to remove every challenge value.</param>
        /// <param name="dnsChallengeService">The service managing challenge records.</param>
        /// <param name="httpContext">The HTTP context carrying the authenticated client.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="cancellationToken">Cancelled when the client goes away.</param>
        /// <returns>The result of the request.</returns>
        public static async Task<IResult> HandleDeleteChallengeAsync(
            string? domain,
            string? value,
            IDnsChallengeService dnsChallengeService,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return Results.BadRequest(new { error = "Domain is required." });

            // A query string that carries the key with nothing after it binds to an empty string, not
            // to null, which is what "&value=$CERTBOT_VALIDATION" becomes when the variable is unset.
            // That would match no value and report the removal of nothing as a success, so it is
            // refused: leaving the key out entirely is how every value is asked for
            if (value != null && string.IsNullOrWhiteSpace(value))
                return Results.BadRequest(new { error = "Challenge value cannot be blank. Leave the value parameter out to remove every value." });

            if (!ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, domain, out string? authorizedDomain))
                return ClientRequest.NotAuthorizedForDomain(domain);

            ILogger logger = loggerFactory.CreateLogger(typeof(DnsChallengeEndpoints));

            return await ClientRequest.RunAsync(logger, "remove the challenge records", authorizedDomain, cancellationToken, async () =>
            {
                int deleted = await dnsChallengeService.DeleteChallengeRecordsAsync(authorizedDomain, value, cancellationToken);
                string recordName = AcmeChallenge.RecordNameFor(authorizedDomain);

                logger.LogInformation(
                    "Client {ClientName} removed {DeletedCount} challenge record(s) at {RecordName}",
                    ClientRequest.DescribeClient(httpContext),
                    deleted,
                    recordName);

                return Results.Ok(new
                {
                    domain = authorizedDomain,
                    recordName,
                    deleted
                });
            });
        }
    }
}
