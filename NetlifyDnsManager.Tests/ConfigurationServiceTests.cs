using Microsoft.Extensions.Logging.Abstractions;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests that optional environment variables reach the configuration, and that an unset or
    /// unparsable variable falls back to its default instead of failing.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ConfigurationServiceTests
    {
        private const string AccessTokenVariable = "NETLIFY_ACCESS_TOKEN";
        private const string CheckIntervalVariable = "CHECK_INTERVAL";
        private const string EnableLoggingVariable = "ENABLE_LOGGING";
        private const string ProxyModeVariable = "PROXY_MODE";
        private const string FirstDomainVariable = "DOMAIN_01";
        private const string ProxyServerUrlVariable = "PROXY_SERVER_URL";
        private const string ProxyApiKeyVariable = "PROXY_API_KEY";

        private static readonly string[] ManagedVariables =
        {
            AccessTokenVariable,
            CheckIntervalVariable,
            EnableLoggingVariable,
            ProxyModeVariable,
            FirstDomainVariable,
            ProxyServerUrlVariable,
            ProxyApiKeyVariable
        };

        private readonly Dictionary<string, string?> _originalValues = new Dictionary<string, string?>();

        private ConfigurationService _configurationService = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            foreach (string variable in ManagedVariables)
            {
                _originalValues[variable] = Environment.GetEnvironmentVariable(variable);
                Environment.SetEnvironmentVariable(variable, null);
            }

            // Required in none mode, which is what an unset PROXY_MODE means
            Environment.SetEnvironmentVariable(AccessTokenVariable, "a-test-access-token-value");
            Environment.SetEnvironmentVariable(FirstDomainVariable, "host.example.com");

            _configurationService = new ConfigurationService(NullLogger<ConfigurationService>.Instance);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            foreach (KeyValuePair<string, string?> originalValue in _originalValues)
            {
                Environment.SetEnvironmentVariable(originalValue.Key, originalValue.Value);
            }
        }

        [TestMethod]
        public void GetConfiguration_WithNothingOptionalSet_UsesTheDefaults()
        {
            // Act
            ApplicationConfiguration configuration = _configurationService.GetConfiguration();

            // Assert
            Assert.AreEqual(1800, configuration.CheckIntervalSeconds);
            Assert.IsTrue(configuration.EnableLogging);
            Assert.AreEqual(ProxyMode.None, configuration.ProxyMode);
            CollectionAssert.AreEqual(new List<string> { "host.example.com" }, configuration.Domains);
        }

        [TestMethod]
        public void GetConfiguration_WithOptionalValuesSet_UsesThem()
        {
            // Arrange - values that differ from every default, so a missed read is visible
            Environment.SetEnvironmentVariable(CheckIntervalVariable, "300");
            Environment.SetEnvironmentVariable(EnableLoggingVariable, "false");
            Environment.SetEnvironmentVariable(ProxyModeVariable, "client");
            Environment.SetEnvironmentVariable(ProxyServerUrlVariable, "http://localhost:5050/");
            Environment.SetEnvironmentVariable(ProxyApiKeyVariable, "a-test-api-key");

            // Act
            ApplicationConfiguration configuration = _configurationService.GetConfiguration();

            // Assert
            Assert.AreEqual(300, configuration.CheckIntervalSeconds);
            Assert.IsFalse(configuration.EnableLogging);
            Assert.AreEqual(ProxyMode.Client, configuration.ProxyMode);
        }

        [TestMethod]
        public void GetConfiguration_WithUnparsableOptionalValues_UsesTheDefaults()
        {
            // Arrange
            Environment.SetEnvironmentVariable(CheckIntervalVariable, "not-a-number");
            Environment.SetEnvironmentVariable(EnableLoggingVariable, "not-a-boolean");
            Environment.SetEnvironmentVariable(ProxyModeVariable, "not-a-mode");

            // Act
            ApplicationConfiguration configuration = _configurationService.GetConfiguration();

            // Assert
            Assert.AreEqual(1800, configuration.CheckIntervalSeconds);
            Assert.IsTrue(configuration.EnableLogging);
            Assert.AreEqual(ProxyMode.None, configuration.ProxyMode);
        }
    }
}
