using EasyReasy.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using NetlifyDnsManager.Configuration;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Live integration tests for the NetlifyService using real Netlify API.
    /// </summary>
    [TestClass]
    public class NetlifyServiceTests
    {
        private INetlifyService _netlifyService = null!;
        private string _testDomain = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Load environment variables from test configuration file
            string environmentFilePath = "environment-variables.txt";
            string absolutePath = Path.GetFullPath(environmentFilePath);
            Console.WriteLine($"Loading environment variables from: {absolutePath}");

            EnvironmentVariableHelper.LoadVariablesFromFile(environmentFilePath);

            // Validate environment variables from both main project and test project
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestEnvironmentVariables));

            // Set up dependency injection
            ServiceCollection services = new ServiceCollection();
            services.AddHttpClient();
            services.AddSingleton<INetlifyService>(serviceProvider =>
            {
                string accessToken = EnvironmentVariables.NetlifyAccessToken.GetValue();
                HttpClient httpClient = serviceProvider.GetRequiredService<HttpClient>();
                return new NetlifyService(accessToken, httpClient);
            });

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            _netlifyService = serviceProvider.GetRequiredService<INetlifyService>();

            // Get test domain from environment variables
            _testDomain = TestEnvironmentVariables.TestDomain.GetValue();
        }

        [TestMethod]
        public async Task GetAllDnsRecordsAsync_WithValidDomain_ReturnsDnsRecords()
        {
            // Act
            NetlifyDnsRecords result = await _netlifyService.GetAllDnsRecordsAsync(_testDomain);

            Console.WriteLine(result);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Records);
            // Note: The result might be empty if no DNS records exist for the domain
        }

        [TestMethod]
        public async Task AddDnsRecordAsync_WithValidParameters_CreatesDnsRecord()
        {
            // Arrange
            string testHostname = $"test.{_testDomain}";
            string recordType = "A";
            string recordValue = "192.168.1.1";
            long ttl = 3600;

            // Act
            NetlifyDnsRecord result = await _netlifyService.AddDnsRecordAsync(testHostname, _testDomain, recordType, recordValue, ttl);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(testHostname, result.Hostname);
            Assert.AreEqual(recordType, result.Type);
            Assert.AreEqual(recordValue, result.Value);
            Assert.AreEqual(ttl, result.Ttl);
            Assert.IsFalse(string.IsNullOrEmpty(result.Id));

            // Cleanup - delete the record we just created
            await _netlifyService.DeleteDnsRecordAsync(result);
        }

        [TestMethod]
        public async Task DeleteDnsRecordAsync_WithValidRecord_DeletesDnsRecord()
        {
            // Arrange - first create a record to delete
            string testHostname = $"delete-test.{_testDomain}";
            string recordType = "A";
            string recordValue = "192.168.1.2";
            long ttl = 3600;

            NetlifyDnsRecord createdRecord = await _netlifyService.AddDnsRecordAsync(testHostname, _testDomain, recordType, recordValue, ttl);

            // Act
            await _netlifyService.DeleteDnsRecordAsync(createdRecord);

            // Assert - verify the record was deleted by trying to get all records
            // and ensuring our test record is not in the list
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(_testDomain);
            bool recordStillExists = allRecords.Records.Any(r => r.Id == createdRecord.Id);
            Assert.IsFalse(recordStillExists, "The DNS record should have been deleted");
        }

        [TestMethod]
        public async Task AddAndDeleteDnsRecord_CompleteWorkflow_Succeeds()
        {
            // Arrange
            string testHostname = $"workflow-test.{_testDomain}";
            string recordType = "CNAME";
            string recordValue = "www.example.com";
            long ttl = 1800;

            // Act & Assert - Add record
            NetlifyDnsRecord addedRecord = await _netlifyService.AddDnsRecordAsync(testHostname, _testDomain, recordType, recordValue, ttl);
            Assert.IsNotNull(addedRecord);
            Assert.AreEqual(testHostname, addedRecord.Hostname);
            Assert.AreEqual(recordType, addedRecord.Type);
            Assert.AreEqual(recordValue, addedRecord.Value);

            // Act & Assert - Delete record
            await _netlifyService.DeleteDnsRecordAsync(addedRecord);

            // Verify deletion
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(_testDomain);
            bool recordStillExists = allRecords.Records.Any(r => r.Id == addedRecord.Id);
            Assert.IsFalse(recordStillExists, "The DNS record should have been deleted");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task GetAllDnsRecordsAsync_WithEmptyDomain_ThrowsArgumentException()
        {
            // Act
            await _netlifyService.GetAllDnsRecordsAsync("");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task AddDnsRecordAsync_WithEmptyHostname_ThrowsArgumentException()
        {
            // Act
            await _netlifyService.AddDnsRecordAsync("", _testDomain, "A", "192.168.1.1", 3600);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task AddDnsRecordAsync_WithEmptyDomain_ThrowsArgumentException()
        {
            // Act
            await _netlifyService.AddDnsRecordAsync($"test.{_testDomain}", "", "A", "192.168.1.1", 3600);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task AddDnsRecordAsync_WithEmptyType_ThrowsArgumentException()
        {
            // Act
            await _netlifyService.AddDnsRecordAsync($"test.{_testDomain}", _testDomain, "", "192.168.1.1", 3600);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task AddDnsRecordAsync_WithEmptyValue_ThrowsArgumentException()
        {
            // Act
            await _netlifyService.AddDnsRecordAsync($"test.{_testDomain}", _testDomain, "A", "", 3600);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DeleteDnsRecordAsync_WithNullRecord_ThrowsArgumentNullException()
        {
            // Act
            await _netlifyService.DeleteDnsRecordAsync(null!);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public async Task DeleteDnsRecordAsync_WithRecordWithoutId_ThrowsArgumentException()
        {
            // Arrange
            NetlifyDnsRecord recordWithoutId = new NetlifyDnsRecord(
                hostname: "test.example.com",
                type: "A",
                ttl: 3600,
                priority: null,
                weight: null,
                port: null,
                flag: null,
                tag: null,
                id: "", // Empty ID
                siteId: null,
                dnsZoneId: "test_zone",
                errors: new List<object>(),
                managed: false,
                value: "192.168.1.1");

            // Act
            await _netlifyService.DeleteDnsRecordAsync(recordWithoutId);
        }
    }
}