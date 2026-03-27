using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Represents the configuration for authorized proxy clients.
    /// </summary>
    public class ClientsConfiguration
    {
        /// <summary>
        /// The list of authorized clients.
        /// </summary>
        [JsonPropertyName("clients")]
        public List<ClientEntry> Clients { get; set; } = new List<ClientEntry>();

        /// <summary>
        /// Loads the clients configuration from a JSON file.
        /// </summary>
        /// <param name="filePath">The path to the JSON configuration file.</param>
        /// <returns>The loaded clients configuration.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
        /// <exception cref="JsonException">Thrown when the file contents cannot be deserialized.</exception>
        public static ClientsConfiguration FromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Clients configuration file not found: {filePath}", filePath);

            string json = File.ReadAllText(filePath);
            ClientsConfiguration? result = JsonSerializer.Deserialize<ClientsConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new JsonException($"Failed to deserialize clients configuration from: {filePath}");

            return result;
        }

        /// <summary>
        /// Finds a client entry by API key using constant-time comparison.
        /// </summary>
        /// <param name="apiKey">The API key to search for.</param>
        /// <returns>The matching client entry, or null if not found.</returns>
        public ClientEntry? FindByApiKey(string apiKey)
        {
            byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);

            return Clients.FirstOrDefault(c =>
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(c.ApiKey),
                    apiKeyBytes));
        }
    }
}
