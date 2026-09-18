using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the rules of an ACME DNS-01 challenge record: the name it sits at, and how a value
    /// published in a zone reads back.
    /// </summary>
    [TestClass]
    public class AcmeChallengeTests
    {
        [TestMethod]
        public void RecordNameFor_PutsTheAcmeLabelBelowTheDomain()
        {
            // ACME looks the value up at exactly this name
            Assert.AreEqual("_acme-challenge.host.example.com", AcmeChallenge.RecordNameFor("host.example.com"));
        }

        [TestMethod]
        public void RecordNameFor_KeepsTheDomainAsGiven()
        {
            // The caller passes the configured spelling, and the record is named after it
            Assert.AreEqual("_acme-challenge.Host.Example.COM", AcmeChallenge.RecordNameFor("Host.Example.COM"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void RecordNameFor_WithBlankDomain_Throws(string? domain)
        {
            Assert.ThrowsException<ArgumentException>(() => AcmeChallenge.RecordNameFor(domain!));
        }

        [TestMethod]
        public void ReadValue_WithAPlainValue_ReturnsItUnchanged()
        {
            Assert.AreEqual("a-challenge-value", AcmeChallenge.ReadValue("a-challenge-value"));
        }

        [TestMethod]
        public void ReadValue_WithAQuotedValue_ReturnsWhatItPublishes()
        {
            // A zone may hand a TXT value back in the quotes its zone file uses
            Assert.AreEqual("a-challenge-value", AcmeChallenge.ReadValue("\"a-challenge-value\""));
        }

        [TestMethod]
        public void ReadValue_WithQuotesInsideTheValue_KeepsThem()
        {
            // Only quoting around the whole value is quoting; anything else is the value itself
            Assert.AreEqual("a-\"quoted\"-part", AcmeChallenge.ReadValue("a-\"quoted\"-part"));
            Assert.AreEqual("\"", AcmeChallenge.ReadValue("\""));
        }

        [TestMethod]
        public void MaxValueLength_IsTheLongestASingleTxtStringCanBe()
        {
            Assert.AreEqual(255, AcmeChallenge.MaxValueLength);
        }

        [TestMethod]
        public void MaxValuesPerName_LeavesRoomForANameAndItsWildcard()
        {
            // A certificate covering a name and its wildcard publishes two values at one name
            Assert.IsTrue(AcmeChallenge.MaxValuesPerName >= 2, "Two concurrent challenges at one name must be possible.");
        }
    }
}
