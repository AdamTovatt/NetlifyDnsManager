using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;
using System.Net;
using System.Security.Claims;

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
                .RequireAuthorization("ApiKeyOnly");

            return app;
        }

        private static async Task<IResult> HandleDnsUpdateAsync(
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

            // Check that the authenticated client is allowed to update this domain
            IEnumerable<string> allowedDomains = httpContext.User.Claims
                .Where(c => c.Type == "allowed_domain")
                .Select(c => c.Value);

            if (!allowedDomains.Contains(request.Domain, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Json(
                    new { error = $"Not authorized to update domain: {request.Domain}" },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                bool updated = await dnsUpdateService.UpdateDnsRecordAsync(request.Domain, request.Ip);
                return Results.Ok(new
                {
                    domain = request.Domain,
                    ip = request.Ip,
                    updated
                });
            }
            catch (Exception ex)
            {
                ILogger logger = loggerFactory.CreateLogger("DnsUpdateEndpoints");
                logger.LogError(ex, "Failed to update DNS record for domain {Domain}", request.Domain);

                return Results.Json(
                    new { error = $"Failed to update DNS record: {ex.Message}" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
