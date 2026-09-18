using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using System.Net;

namespace NetlifyDnsManager.Endpoints
{
    /// <summary>
    /// Extension methods for mapping DNS update API endpoints.
    /// </summary>
    public static class DnsUpdateEndpoints
    {
        /// <summary>
        /// Maps the DNS update endpoint for proxy clients.
        /// Requires API key authentication.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication MapDnsUpdateEndpoints(this WebApplication app)
        {
            app.MapPost("/api/dns/update", HandleDnsUpdateAsync)
                .RequireAuthorization(ClientRequest.ApiKeyPolicy);

            return app;
        }

        /// <summary>
        /// Updates the A record for a domain the authenticated client is authorized for.
        /// </summary>
        /// <param name="request">The update request.</param>
        /// <param name="dnsUpdateService">The service updating DNS records.</param>
        /// <param name="httpContext">The HTTP context carrying the authenticated client.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <returns>The result of the request.</returns>
        public static async Task<IResult> HandleDnsUpdateAsync(
            DnsUpdateRequest request,
            IDnsUpdateService dnsUpdateService,
            HttpContext httpContext,
            ILoggerFactory loggerFactory)
        {
            if (string.IsNullOrWhiteSpace(request.Domain))
                return Results.BadRequest(new { error = "Domain is required." });

            if (string.IsNullOrWhiteSpace(request.Ip))
                return Results.BadRequest(new { error = "IP address is required." });

            if (!IPAddress.TryParse(request.Ip, out _))
                return Results.BadRequest(new { error = "Invalid IP address format." });

            if (!ClientDomainAuthorization.TryGetAuthorizedDomain(httpContext.User, request.Domain, out string? domain))
                return ClientRequest.NotAuthorizedForDomain(request.Domain);

            ILogger logger = loggerFactory.CreateLogger(typeof(DnsUpdateEndpoints));

            return await ClientRequest.RunAsync(logger, "update the DNS record", domain, async () =>
            {
                bool updated = await dnsUpdateService.UpdateDnsRecordAsync(domain, request.Ip);

                logger.LogInformation(
                    "Client {ClientName} reported {IpAddress} for {Domain} (updated: {Updated})",
                    ClientRequest.DescribeClient(httpContext),
                    request.Ip,
                    domain,
                    updated);

                return Results.Ok(new
                {
                    domain,
                    ip = request.Ip,
                    updated
                });
            });
        }
    }
}
