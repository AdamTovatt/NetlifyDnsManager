using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for <see cref="ClientsConfiguration"/> loading and client lookup.
    /// </summary>
    [TestClass]
    public class ClientsConfigurationTests
    {
        private string _tempFilePath = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _tempFilePath = Path.GetTempFileName();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }

        [TestMethod]
        public void FromFile_WithValidJson_LoadsClientsCorrectly()
        {
            // Arrange
            string json = """
            {
                "clients": [
                    {
                        "apiKey": "test-key-001",
                        "allowedDomains": ["friend.sakurapi.se", "sub.sakurapi.se"],
                        "name": "Friend"
                    },
                    {
                        "apiKey": "test-key-002",
                        "allowedDomains": ["other.sakurapi.se"],
                        "name": "Other"
                    }
                ]
            }
            """;
            File.WriteAllText(_tempFilePath, json);

            // Act
            ClientsConfiguration config = ClientsConfiguration.FromFile(_tempFilePath);

            // Assert
            Assert.AreEqual(2, config.Clients.Count);
            Assert.AreEqual("test-key-001", config.Clients[0].ApiKey);
            Assert.AreEqual(2, config.Clients[0].AllowedDomains.Count);
            Assert.AreEqual("Friend", config.Clients[0].Name);
            Assert.AreEqual("test-key-002", config.Clients[1].ApiKey);
            Assert.AreEqual(1, config.Clients[1].AllowedDomains.Count);
        }

        [TestMethod]
        public void FindByApiKey_WithMatchingKey_ReturnsClient()
        {
            // Arrange
            string json = """
            {
                "clients": [
                    {
                        "apiKey": "test-key-001",
                        "allowedDomains": ["friend.sakurapi.se"],
                        "name": "Friend"
                    }
                ]
            }
            """;
            File.WriteAllText(_tempFilePath, json);
            ClientsConfiguration config = ClientsConfiguration.FromFile(_tempFilePath);

            // Act
            ClientEntry? result = config.FindByApiKey("test-key-001");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Friend", result.Name);
            Assert.IsTrue(result.AllowedDomains.Contains("friend.sakurapi.se"));
        }

        [TestMethod]
        public void FindByApiKey_WithUnknownKey_ReturnsNull()
        {
            // Arrange
            string json = """
            {
                "clients": [
                    {
                        "apiKey": "test-key-001",
                        "allowedDomains": ["friend.sakurapi.se"]
                    }
                ]
            }
            """;
            File.WriteAllText(_tempFilePath, json);
            ClientsConfiguration config = ClientsConfiguration.FromFile(_tempFilePath);

            // Act
            ClientEntry? result = config.FindByApiKey("unknown-key");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void FromFile_WithMissingFile_ThrowsFileNotFoundException()
        {
            // Act
            ClientsConfiguration.FromFile("/nonexistent/path/clients.json");
        }

        [TestMethod]
        public void FromFile_WithEmptyClientsList_LoadsEmptyList()
        {
            // Arrange
            string json = """
            {
                "clients": []
            }
            """;
            File.WriteAllText(_tempFilePath, json);

            // Act
            ClientsConfiguration config = ClientsConfiguration.FromFile(_tempFilePath);

            // Assert
            Assert.IsNotNull(config.Clients);
            Assert.AreEqual(0, config.Clients.Count);
        }

        [TestMethod]
        public void FindByApiKey_WithMultipleClients_ReturnsCorrectClient()
        {
            // Arrange
            string json = """
            {
                "clients": [
                    {
                        "apiKey": "key-alpha",
                        "allowedDomains": ["alpha.sakurapi.se"],
                        "name": "Alpha"
                    },
                    {
                        "apiKey": "key-beta",
                        "allowedDomains": ["beta.sakurapi.se"],
                        "name": "Beta"
                    },
                    {
                        "apiKey": "key-gamma",
                        "allowedDomains": ["gamma.sakurapi.se", "gamma2.sakurapi.se"],
                        "name": "Gamma"
                    }
                ]
            }
            """;
            File.WriteAllText(_tempFilePath, json);
            ClientsConfiguration config = ClientsConfiguration.FromFile(_tempFilePath);

            // Act
            ClientEntry? beta = config.FindByApiKey("key-beta");
            ClientEntry? gamma = config.FindByApiKey("key-gamma");

            // Assert
            Assert.IsNotNull(beta);
            Assert.AreEqual("Beta", beta.Name);
            Assert.AreEqual(1, beta.AllowedDomains.Count);

            Assert.IsNotNull(gamma);
            Assert.AreEqual("Gamma", gamma.Name);
            Assert.AreEqual(2, gamma.AllowedDomains.Count);
        }
    }
}
