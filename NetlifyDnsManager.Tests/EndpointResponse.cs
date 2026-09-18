using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// What a client receives from an endpoint: the status code and the body, so a test can check
    /// that the answer the endpoint computed actually reaches the client.
    /// </summary>
    /// <param name="StatusCode">The response status code.</param>
    /// <param name="Body">The response body as written.</param>
    internal sealed record EndpointResponse(int StatusCode, string Body)
    {
        /// <summary>
        /// Runs an endpoint result against the context it was called with and reads what it wrote.
        /// </summary>
        /// <param name="result">The result the endpoint returned.</param>
        /// <param name="httpContext">The context the endpoint was called with.</param>
        /// <returns>The response.</returns>
        public static async Task<EndpointResponse> ReadAsync(IResult result, HttpContext httpContext)
        {
            await result.ExecuteAsync(httpContext);

            httpContext.Response.Body.Position = 0;
            using StreamReader reader = new StreamReader(httpContext.Response.Body);
            string body = await reader.ReadToEndAsync();

            return new EndpointResponse(httpContext.Response.StatusCode, body);
        }

        /// <summary>
        /// Reads a field of the JSON body.
        /// </summary>
        /// <param name="fieldName">The field to read.</param>
        /// <returns>
        /// A string field's text, or any other field as it is written in the JSON, or null if the
        /// body has no such field.
        /// </returns>
        public string? Field(string fieldName)
        {
            using JsonDocument document = JsonDocument.Parse(Body);

            if (!document.RootElement.TryGetProperty(fieldName, out JsonElement field))
                return null;

            return field.ValueKind == JsonValueKind.String ? field.GetString() : field.GetRawText();
        }
    }
}
