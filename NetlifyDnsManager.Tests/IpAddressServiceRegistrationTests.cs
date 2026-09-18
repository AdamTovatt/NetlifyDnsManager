using Microsoft.Extensions.DependencyInjection;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests that exactly one address source is registered, and that configuring an interface
    /// replaces the public IP services rather than joining them.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class IpAddressServiceRegistrationTests
    {
        private const string IpSourceInterfaceVariable = "IP_SOURCE_INTERFACE";

        private string? _originalIpSourceInterface;

        [TestInitialize]
        public void TestInitialize()
        {
            _originalIpSourceInterface = Environment.GetEnvironmentVariable(IpSourceInterfaceVariable);
            Environment.SetEnvironmentVariable(IpSourceInterfaceVariable, null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(IpSourceInterfaceVariable, _originalIpSourceInterface);
        }

        [TestMethod]
        public void AddIpAddressService_WithoutInterfaceName_UsesPublicIpServices()
        {
            // Act
            using ServiceProvider serviceProvider = BuildServiceProvider(interfaceName: null);
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.IsInstanceOfType(ipAddressService, typeof(CompoundIpAddressService));
        }

        [TestMethod]
        public void AddIpAddressService_WithInterfaceName_UsesTheInterfaceReader()
        {
            // Act
            using ServiceProvider serviceProvider = BuildServiceProvider("lo");
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.IsInstanceOfType(ipAddressService, typeof(NetworkInterfaceIpAddressService));
        }

        [TestMethod]
        public void AddIpAddressService_WithInterfaceName_RegistersNoPublicIpSourceAtAll()
        {
            // Act
            using ServiceProvider serviceProvider = BuildServiceProvider("lo");
            List<IIpAddressService> registeredServices = serviceProvider.GetServices<IIpAddressService>().ToList();

            // Assert - a public source registered alongside the interface reader would be reached
            // whenever the interface read failed, publishing this host's public address
            Assert.AreEqual(1, registeredServices.Count, "Exactly one address source must be registered.");
            Assert.IsFalse(
                registeredServices.Any(service => service is CompoundIpAddressService || service is IpAddressService),
                "No public IP source may be registered when an interface is configured.");
        }

        [TestMethod]
        public void AddIpAddressService_WithoutInterfaceName_RegistersNoInterfaceReader()
        {
            // Act
            using ServiceProvider serviceProvider = BuildServiceProvider(interfaceName: null);
            List<IIpAddressService> registeredServices = serviceProvider.GetServices<IIpAddressService>().ToList();

            // Assert
            Assert.AreEqual(1, registeredServices.Count, "Exactly one address source must be registered.");
            Assert.IsFalse(
                registeredServices.Any(service => service is NetworkInterfaceIpAddressService),
                "No interface reader may be registered when no interface is configured.");
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public void AddIpAddressService_WithBlankInterfaceName_UsesPublicIpServices(string interfaceName)
        {
            // Act - a blank name is not a configured interface, and must not reach the reader's own guard
            using ServiceProvider serviceProvider = BuildServiceProvider(interfaceName);
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.IsInstanceOfType(ipAddressService, typeof(CompoundIpAddressService));
        }

        [TestMethod]
        public async Task AddIpAddressService_WithPaddedInterfaceName_ReadsTheNamedInterface()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("The loopback interface is only named 'lo' on Linux.");
            }

            // Act - systemd unit files keep surrounding whitespace in the value
            using ServiceProvider serviceProvider = BuildServiceProvider(" lo ");
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.AreEqual("127.0.0.1", await ipAddressService.GetIpAddressAsync());
        }

        [TestMethod]
        public void AddIpAddressService_WithInterfaceName_DescribesTheInterfaceAsTheSource()
        {
            // Act
            using ServiceProvider serviceProvider = BuildServiceProvider("lo");
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert - the startup log names the interface, so an operator can see which source is live
            StringAssert.Contains(ipAddressService.Description, "lo");
        }

        [TestMethod]
        public void AddIpAddressService_WithoutInterfaceName_DescribesThePublicServicesAsTheSource()
        {
            // Act
            using ServiceProvider serviceProvider = BuildServiceProvider(interfaceName: null);
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.AreEqual("public IP services", ipAddressService.Description);
        }

        [TestMethod]
        public void AddIpAddressService_WithIpSourceInterfaceSet_UsesTheInterfaceReader()
        {
            // Arrange - the configured path, which reads the environment variable itself
            Environment.SetEnvironmentVariable(IpSourceInterfaceVariable, "lo");

            ServiceCollection services = new ServiceCollection();
            services.AddHttpClient();
            services.AddIpAddressService();

            // Act
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.IsInstanceOfType(ipAddressService, typeof(NetworkInterfaceIpAddressService));
            StringAssert.Contains(ipAddressService.Description, "lo");
        }

        [TestMethod]
        public void AddIpAddressService_WithoutIpSourceInterfaceSet_UsesPublicIpServices()
        {
            // Arrange - the variable is cleared by TestInitialize
            ServiceCollection services = new ServiceCollection();
            services.AddHttpClient();
            services.AddIpAddressService();

            // Act
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IIpAddressService ipAddressService = serviceProvider.GetRequiredService<IIpAddressService>();

            // Assert
            Assert.IsInstanceOfType(ipAddressService, typeof(CompoundIpAddressService));
        }

        private static ServiceProvider BuildServiceProvider(string? interfaceName)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddHttpClient();
            services.AddIpAddressService(interfaceName);

            return services.BuildServiceProvider();
        }
    }
}
