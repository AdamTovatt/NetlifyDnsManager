using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetlifyDnsManager.Endpoints;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests what the mapping itself publishes: the paths clients are documented to call, and the
    /// authentication in front of them. The handlers are driven directly everywhere else, which leaves
    /// both of those unguarded.
    /// </summary>
    [TestClass]
    public class EndpointMappingTests
    {
        // The policy Program registers for API key clients, spelled out because the constant the
        // endpoints use is internal to the application
        private const string ApiKeyPolicy = "ApiKeyOnly";

        [TestMethod]
        public async Task MapDnsChallengeEndpoints_PublishesOnePathForBothVerbs()
        {
            // The path is documented, and is written without a trailing slash on purpose
            List<RouteEndpoint> endpoints = await MappedEndpointsAsync(app => app.MapDnsChallengeEndpoints());

            CollectionAssert.AreEquivalent(
                new[] { "POST /api/dns/challenge", "DELETE /api/dns/challenge" },
                endpoints.Select(Describe).ToList());
        }

        [TestMethod]
        public async Task MapDnsUpdateEndpoints_PublishesTheDocumentedPath()
        {
            List<RouteEndpoint> endpoints = await MappedEndpointsAsync(app => app.MapDnsUpdateEndpoints());

            CollectionAssert.AreEquivalent(
                new[] { "POST /api/dns/update" },
                endpoints.Select(Describe).ToList());
        }

        [TestMethod]
        public async Task MapDnsChallengeEndpoints_PutsTheApiKeyPolicyInFrontOfEveryRoute()
        {
            // Without this the handlers' own claim checks are all that stands there, and an unauthenticated
            // request reaches them carrying no claims at all
            List<RouteEndpoint> endpoints = await MappedEndpointsAsync(app => app.MapDnsChallengeEndpoints());

            foreach (RouteEndpoint endpoint in endpoints)
            {
                CollectionAssert.Contains(RequiredPolicies(endpoint), ApiKeyPolicy, $"{Describe(endpoint)} is not behind the API key policy.");
            }
        }

        [TestMethod]
        public async Task MapDnsUpdateEndpoints_PutsTheApiKeyPolicyInFrontOfTheRoute()
        {
            List<RouteEndpoint> endpoints = await MappedEndpointsAsync(app => app.MapDnsUpdateEndpoints());

            foreach (RouteEndpoint endpoint in endpoints)
            {
                CollectionAssert.Contains(RequiredPolicies(endpoint), ApiKeyPolicy, $"{Describe(endpoint)} is not behind the API key policy.");
            }
        }

        private static async Task<List<RouteEndpoint>> MappedEndpointsAsync(Action<WebApplication> map)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            // Building a route reads the handler's parameters and asks where each one comes from, so the
            // services it takes have to be resolvable. Nothing here is ever called: no request is made
            builder.Services.AddSingleton(Mock.Of<IDnsUpdateService>());
            builder.Services.AddSingleton(Mock.Of<IDnsChallengeService>());

            WebApplication app = builder.Build();

            await using (app)
            {
                map(app);

                return ((IEndpointRouteBuilder)app).DataSources
                    .SelectMany(dataSource => dataSource.Endpoints)
                    .OfType<RouteEndpoint>()
                    .ToList();
            }
        }

        private static string Describe(RouteEndpoint endpoint)
        {
            HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

            // The raw pattern, so that a trailing slash or a moved path is a difference and not hidden
            return $"{string.Join(",", methods?.HttpMethods ?? Array.Empty<string>())} {endpoint.RoutePattern.RawText}";
        }

        private static List<string?> RequiredPolicies(RouteEndpoint endpoint)
        {
            return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(authorizeData => authorizeData.Policy)
                .ToList();
        }
    }
}
