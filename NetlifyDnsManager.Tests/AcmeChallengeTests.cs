using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the rules of an ACME DNS-01 challenge record: the name it sits at, and how much a single
    /// record may carry.
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
        public void MaxValueBytes_IsTheLongestASingleTxtStringCanBe()
        {
            Assert.AreEqual(255, AcmeChallenge.MaxValueBytes);
        }

        [TestMethod]
        public void IsValueTooLong_AtTheLimit_IsFalse()
        {
            // A value of exactly the limit still fits in one TXT string
            Assert.IsFalse(AcmeChallenge.IsValueTooLong(new string('a', AcmeChallenge.MaxValueBytes)));
        }

        [TestMethod]
        public void IsValueTooLong_OneCharacterOverTheLimit_IsTrue()
        {
            Assert.IsTrue(AcmeChallenge.IsValueTooLong(new string('a', AcmeChallenge.MaxValueBytes + 1)));
        }

        [TestMethod]
        public void IsValueTooLong_MeasuresBytesRatherThanCharacters()
        {
            // 200 characters, each two bytes: a character count would let this through at 400 bytes,
            // and the record would then be refused by DNS rather than by us
            string twoByteCharacters = new string('ä', 200);

            Assert.IsTrue(twoByteCharacters.Length <= AcmeChallenge.MaxValueBytes, "The value has to be short in characters for this to test anything.");
            Assert.IsTrue(AcmeChallenge.IsValueTooLong(twoByteCharacters));
        }

        [TestMethod]
        public void MaxValuesPerName_LeavesRoomForANameAndItsWildcard()
        {
            // A certificate covering a name and its wildcard publishes two values at one name
            Assert.IsTrue(AcmeChallenge.MaxValuesPerName >= 2, "Two concurrent challenges at one name must be possible.");
        }
    }
}
