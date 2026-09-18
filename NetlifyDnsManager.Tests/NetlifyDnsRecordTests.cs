using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests how a record the provider returned reads back, which is what finding a published value
    /// again depends on.
    /// </summary>
    [TestClass]
    public class NetlifyDnsRecordTests
    {
        [TestMethod]
        public void ReadTextValue_WithAPlainValue_ReturnsItUnchanged()
        {
            Assert.AreEqual("a-challenge-value", TxtRecord("a-challenge-value").ReadTextValue());
        }

        [TestMethod]
        public void ReadTextValue_WithAQuotedValue_ReturnsWhatItPublishes()
        {
            // A zone may hand a TXT value back in the quotes its zone file uses
            Assert.AreEqual("a-challenge-value", TxtRecord("\"a-challenge-value\"").ReadTextValue());
        }

        [TestMethod]
        public void ReadTextValue_WithQuotesInsideTheValue_KeepsThem()
        {
            // Only quoting around the whole value is quoting; anything else is the value itself
            Assert.AreEqual("a-\"quoted\"-part", TxtRecord("a-\"quoted\"-part").ReadTextValue());
            Assert.AreEqual("\"", TxtRecord("\"").ReadTextValue());
        }

        private static NetlifyDnsRecord TxtRecord(string value)
        {
            return DnsRecordFactory.Create("_acme-challenge.host.example.com", "TXT", value);
        }
    }
}
