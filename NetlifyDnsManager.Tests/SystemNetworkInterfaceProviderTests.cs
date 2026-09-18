using NetlifyDnsManager.Services;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests for reading interface addresses from the operating system.
    /// </summary>
    [TestClass]
    public class SystemNetworkInterfaceProviderTests
    {
        private SystemNetworkInterfaceProvider _provider = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _provider = new SystemNetworkInterfaceProvider();
        }

        [TestMethod]
        public void TryGetIpv4Addresses_ExistingInterface_ReturnsThatInterfacesOwnIpv4Addresses()
        {
            // Arrange - pick a real interface and ask the operating system directly what it has
            NetworkInterface? existingInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(candidate => candidate.GetIPProperties().UnicastAddresses
                    .Any(unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork));

            if (existingInterface == null)
            {
                Assert.Inconclusive("This host has no network interface with an IPv4 address.");
            }

            List<string> expectedAddresses = existingInterface!.GetIPProperties().UnicastAddresses
                .Where(unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(unicastAddress => unicastAddress.Address.ToString())
                .ToList();

            // Act
            bool found = _provider.TryGetIpv4Addresses(existingInterface.Name, out IReadOnlyList<IPAddress> addresses);

            // Assert - exactly that interface's addresses, not some other interface's and not its IPv6 ones
            Assert.IsTrue(found);
            CollectionAssert.AreEqual(expectedAddresses, addresses.Select(address => address.ToString()).ToList());
        }

        [TestMethod]
        public void TryGetIpv4Addresses_UnknownInterface_ReturnsFalse()
        {
            // Act
            bool found = _provider.TryGetIpv4Addresses("not-a-real-interface0", out IReadOnlyList<IPAddress> addresses);

            // Assert - false and an empty list mean different things: no such interface versus no address on it
            Assert.IsFalse(found);
            Assert.AreEqual(0, addresses.Count);
        }

        [TestMethod]
        public void TryGetIpv4Addresses_LoopbackInterface_ReturnsTheLoopbackAddress()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("The loopback interface is only named 'lo' on Linux.");
            }

            // Act
            bool found = _provider.TryGetIpv4Addresses("lo", out IReadOnlyList<IPAddress> addresses);

            // Assert
            Assert.IsTrue(found);
            CollectionAssert.Contains(addresses.Select(address => address.ToString()).ToList(), "127.0.0.1");
        }

        [TestMethod]
        public void TryGetIpv4Addresses_NameDifferingInCase_FindsTheInterface()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("The loopback interface is only named 'lo' on Linux.");
            }

            // Act
            bool found = _provider.TryGetIpv4Addresses("LO", out IReadOnlyList<IPAddress> addresses);

            // Assert
            Assert.IsTrue(found);
            CollectionAssert.Contains(addresses.Select(address => address.ToString()).ToList(), "127.0.0.1");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void TryGetIpv4Addresses_BlankInterfaceName_Throws(string? interfaceName)
        {
            // A blank name is a caller mistake, not an interface that does not exist
            Assert.ThrowsException<ArgumentException>(
                () => _provider.TryGetIpv4Addresses(interfaceName!, out _));
        }
    }
}
