using System.Text.Json;

namespace NetlifyDnsManager.Helpers
{
    internal class JsonSettings
    {
        public static JsonSerializerOptions Default { get; set; } = new JsonSerializerOptions()
        {
            TypeInfoResolver = SerializationContext.Default,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
