using NetlifyDnsManager.Models;
using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Helpers
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(NetlifyDnsRecord))]
    [JsonSerializable(typeof(NetlifyDnsRecords))]
    internal partial class SerializationContext : JsonSerializerContext
    {
    }
}
