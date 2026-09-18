using Moq;
using NetlifyDnsManager.Services;
using System.Net;
using System.Reflection;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for reading the reported address from a named local network interface.
    /// </summary>
    [TestClass]
    public class NetworkInterfaceIpAddressServiceTests
    {
        private Mock<INetworkInterfaceProvider> _providerMock = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _providerMock = new Mock<INetworkInterfaceProvider>();
        }

        [TestMethod]
        public async Task GetIpAddressAsync_InterfaceHasIpv4_ReturnsAddressOfConfiguredInterface()
        {
            // Arrange - two interfaces with different addresses, so reading the wrong one is visible
            SetUpInterface("wg0", "10.1.1.7");
            SetUpInterface("eth0", "192.168.50.4");

            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("wg0", _providerMock.Object);

            // Act
            string address = await service.GetIpAddressAsync();

            // Assert
            Assert.AreEqual("10.1.1.7", address);
        }

        [TestMethod]
        public async Task GetIpAddressAsync_InterfaceHasSeveralIpv4Addresses_ReturnsTheFirstOne()
        {
            // Arrange
            SetUpInterface("wg0", "10.1.1.7", "10.2.2.9");

            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("wg0", _providerMock.Object);

            // Act
            string address = await service.GetIpAddressAsync();

            // Assert
            Assert.AreEqual("10.1.1.7", address);
        }

        [TestMethod]
        public async Task GetIpAddressAsync_InterfaceDoesNotExist_ThrowsInsteadOfReturningAnAddress()
        {
            // Arrange - no interface with that name exists
            _providerMock
                .Setup(provider => provider.TryGetIpv4Addresses("wg0", out It.Ref<IReadOnlyList<IPAddress>>.IsAny))
                .Returns(false);

            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("wg0", _providerMock.Object);

            // Act
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.GetIpAddressAsync());

            // Assert - the message says the interface is missing, not that it lacks an address
            StringAssert.Contains(exception.Message, "wg0");
            StringAssert.Contains(exception.Message, "No network interface named");
        }

        [TestMethod]
        public async Task GetIpAddressAsync_InterfaceExistsWithoutIpv4_ThrowsInsteadOfReturningAnAddress()
        {
            // Arrange - the interface exists but has no IPv4 address
            SetUpInterface("wg0");

            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("wg0", _providerMock.Object);

            // Act
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.GetIpAddressAsync());

            // Assert - the message says the interface exists without an address, not that it is missing
            StringAssert.Contains(exception.Message, "wg0");
            StringAssert.Contains(exception.Message, "has no IPv4 address assigned");
        }

        [TestMethod]
        public async Task GetIpAddressAsync_InterfaceHasOnlyLinkLocalAddress_ThrowsInsteadOfPublishingIt()
        {
            // Arrange - an interface assigns itself a 169.254 address when address configuration fails
            SetUpInterface("eth0", "169.254.13.2");

            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("eth0", _providerMock.Object);

            // Act
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.GetIpAddressAsync());

            // Assert
            StringAssert.Contains(exception.Message, "link-local");
        }

        [TestMethod]
        public async Task GetIpAddressAsync_InterfaceHasLinkLocalAndRealAddress_ReturnsTheRealOne()
        {
            // Arrange - the link-local address is listed first
            SetUpInterface("eth0", "169.254.13.2", "192.168.50.4");

            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("eth0", _providerMock.Object);

            // Act
            string address = await service.GetIpAddressAsync();

            // Assert
            Assert.AreEqual("192.168.50.4", address);
        }

        [TestMethod]
        public void Description_NamesTheConfiguredInterface()
        {
            // Arrange
            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("wg0", _providerMock.Object);

            // Assert - this is what the startup log tells the operator the address comes from
            StringAssert.Contains(service.Description, "wg0");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void Constructor_WithBlankInterfaceName_Throws(string? interfaceName)
        {
            // A blank name would silently match nothing, so it is refused up front
            Assert.ThrowsException<ArgumentException>(
                () => new NetworkInterfaceIpAddressService(interfaceName!, _providerMock.Object));
        }

        [TestMethod]
        public void NetworkInterfaceIpAddressService_IsNotGivenAnAddressSourceToFallBackOn()
        {
            // A fallback source would publish the host's public address whenever the interface read
            // failed. This catches such a source being injected or held; it cannot catch one
            // constructed inline inside a method.
            Type serviceType = typeof(NetworkInterfaceIpAddressService);

            IEnumerable<Type> dependencyTypes = serviceType
                .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Concat(serviceType.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType));

            foreach (Type dependencyType in dependencyTypes)
            {
                Assert.IsFalse(
                    typeof(IIpAddressService).IsAssignableFrom(dependencyType),
                    $"{serviceType.Name} must not depend on another {nameof(IIpAddressService)} ({dependencyType.Name}).");

                Assert.IsFalse(
                    typeof(HttpClient).IsAssignableFrom(dependencyType) || typeof(IHttpClientFactory).IsAssignableFrom(dependencyType),
                    $"{serviceType.Name} must not be given a way to reach a remote address service ({dependencyType.Name}).");

                Assert.IsFalse(
                    typeof(IServiceProvider).IsAssignableFrom(dependencyType),
                    $"{serviceType.Name} must not be able to resolve another address service ({dependencyType.Name}).");
            }
        }

        [TestMethod]
        public async Task GetIpAddressAsync_RealLoopbackInterface_ReturnsTheLoopbackAddress()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("The loopback interface is only named 'lo' on Linux.");
            }

            // Arrange - read a real interface through the real provider, with no knowledge of any VPN
            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService("lo", new SystemNetworkInterfaceProvider());

            // Act
            string address = await service.GetIpAddressAsync();

            // Assert
            Assert.AreEqual("127.0.0.1", address);
        }

        [TestMethod]
        public async Task GetIpAddressAsync_RealInterfaceThatDoesNotExist_Throws()
        {
            // Arrange
            NetworkInterfaceIpAddressService service = new NetworkInterfaceIpAddressService(
                "not-a-real-interface0",
                new SystemNetworkInterfaceProvider());

            // Act & Assert
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.GetIpAddressAsync());

            StringAssert.Contains(exception.Message, "No network interface named");
        }

        private void SetUpInterface(string interfaceName, params string[] addresses)
        {
            IReadOnlyList<IPAddress> parsedAddresses = addresses.Select(IPAddress.Parse).ToList();

            _providerMock
                .Setup(provider => provider.TryGetIpv4Addresses(interfaceName, out It.Ref<IReadOnlyList<IPAddress>>.IsAny))
                .Returns(new TryGetIpv4Addresses((string _, out IReadOnlyList<IPAddress> result) =>
                {
                    result = parsedAddresses;
                    return true;
                }));
        }

        private delegate bool TryGetIpv4Addresses(string interfaceName, out IReadOnlyList<IPAddress> addresses);
    }
}
