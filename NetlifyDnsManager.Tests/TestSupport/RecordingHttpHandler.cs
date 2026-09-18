using EasyReasy.Auth.Client;
using System.Net;
using System.Text;

namespace NetlifyDnsManager.Tests.TestSupport
{
    /// <summary>
    /// Records every request a worker makes and answers the authentication request,
    /// so that a report attempt reaches the update endpoint.
    /// </summary>
    internal sealed class RecordingHttpHandler : HttpMessageHandler
    {
        private const string AuthPath = "/api/auth/apikey";
        private const string UpdatePath = "/api/dns/update";

        private readonly List<RecordedRequest> _requests = new List<RecordedRequest>();

        /// <summary>
        /// Gets or sets whether update requests are answered with a server error.
        /// </summary>
        public bool FailUpdateRequests { get; set; }

        /// <summary>
        /// Gets every request received so far.
        /// </summary>
        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the DNS update requests received so far.
        /// </summary>
        public IReadOnlyList<RecordedRequest> UpdateRequests =>
            Requests.Where(request => request.Path.EndsWith(UpdatePath)).ToList();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            string body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (_requests)
            {
                _requests.Add(new RecordedRequest(request.Method.Method, path, body));
            }

            if (path.EndsWith(AuthPath))
            {
                AuthResponse authResponse = new AuthResponse(
                    token: "test-token",
                    expiresAt: DateTime.UtcNow.AddHours(1).ToString("O"));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(authResponse.ToJson(), Encoding.UTF8, "application/json")
                };
            }

            if (FailUpdateRequests)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"test failure\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }

        /// <summary>
        /// A request the handler received.
        /// </summary>
        /// <param name="Method">The HTTP method.</param>
        /// <param name="Path">The request path.</param>
        /// <param name="Body">The request body, empty when there was none.</param>
        internal sealed record RecordedRequest(string Method, string Path, string Body)
        {
            public override string ToString() => $"{Method} {Path}";
        }
    }
}
