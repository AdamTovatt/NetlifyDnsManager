using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Tests the challenge record logic: where the record is written, when it is refused, and that
    /// removal takes away the challenge records and nothing else.
    /// </summary>
    [TestClass]
    public class DnsChallengeServiceTests
    {
        private const string Domain = "host.example.com";
        private const string ChallengeRecordName = "_acme-challenge.host.example.com";
        private const string ChallengeValue = "challenge-value-as-published-by-the-acme-client";
        private const string OtherChallengeValue = "the-challenge-value-of-the-wildcard-name";

        private Mock<INetlifyService> _netlifyServiceMock = null!;
        private DnsChallengeService _service = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _netlifyServiceMock = new Mock<INetlifyService>();
            _service = new DnsChallengeService(_netlifyServiceMock.Object, NullLogger<DnsChallengeService>.Instance);
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WritesATxtRecordAtTheChallengeName()
        {
            // Arrange
            SetUpZone();

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert - name, type and value each in their own place, with a short time to live
            Assert.AreEqual(ChallengeSetResult.Created, result);
            VerifyAdded(ChallengeRecordName, "TXT", ChallengeValue, AcmeChallenge.RecordTtl);
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WhenValueAlreadyPublished_WritesNothing()
        {
            // Arrange
            SetUpZone(Challenge(ChallengeValue));

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert
            Assert.AreEqual(ChallengeSetResult.AlreadyPublished, result);
            VerifyNoRecordAdded();
            VerifyNoRecordDeleted();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WhenTheProviderQuotesTheValue_RecognisesItAsPublished()
        {
            // Arrange - a DNS provider may hand a TXT value back in the quotes its zone file uses
            SetUpZone(DnsRecordFactory.Create(ChallengeRecordName, "TXT", $"\"{ChallengeValue}\"", AcmeChallenge.RecordTtl));

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert - comparing raw values would publish a second copy of the same value
            Assert.AreEqual(ChallengeSetResult.AlreadyPublished, result);
            VerifyNoRecordAdded();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WithASecondValue_KeepsTheFirstOne()
        {
            // Arrange - a wildcard and its base name are validated at the same record name
            SetUpZone(Challenge(OtherChallengeValue));

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert - the existing value survives, so the first validation still passes
            Assert.AreEqual(ChallengeSetResult.Created, result);
            VerifyAdded(ChallengeRecordName, "TXT", ChallengeValue, AcmeChallenge.RecordTtl);
            VerifyNoRecordDeleted();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WhenTheNameAlreadyHoldsTheMaximum_RefusesToAddMore()
        {
            // Arrange - values left behind by earlier challenges that were never cleaned up
            NetlifyDnsRecord[] published = Enumerable.Range(1, AcmeChallenge.MaxValuesPerName)
                .Select(index => Challenge($"a-leftover-challenge-value-{index}"))
                .ToArray();

            SetUpZone(published);

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert - the zone is shared, so one client cannot grow a record set without bound
            Assert.AreEqual(ChallengeSetResult.TooManyValues, result);
            VerifyNoRecordAdded();
            VerifyNoRecordDeleted();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WhenTheNameHoldsOneFewerThanTheMaximum_StillAdds()
        {
            // Arrange - the boundary belongs to the accepted side
            NetlifyDnsRecord[] published = Enumerable.Range(1, AcmeChallenge.MaxValuesPerName - 1)
                .Select(index => Challenge($"a-leftover-challenge-value-{index}"))
                .ToArray();

            SetUpZone(published);

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert
            Assert.AreEqual(ChallengeSetResult.Created, result);
            VerifyAdded(ChallengeRecordName, "TXT", ChallengeValue, AcmeChallenge.RecordTtl);
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_CountsOnlyValuesAtItsOwnRecordName()
        {
            // Arrange - another host's challenges must not fill this name's quota
            NetlifyDnsRecord[] otherHostsChallenges = Enumerable.Range(1, AcmeChallenge.MaxValuesPerName)
                .Select(index => DnsRecordFactory.Create("_acme-challenge.other.example.com", "TXT", $"another-hosts-value-{index}", AcmeChallenge.RecordTtl))
                .ToArray();

            SetUpZone(otherHostsChallenges);

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            // Assert
            Assert.AreEqual(ChallengeSetResult.Created, result);
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WithTooLongValue_ThrowsAndWritesNothing()
        {
            // Arrange
            SetUpZone();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _service.SetChallengeRecordAsync(Domain, new string('a', AcmeChallenge.MaxValueBytes + 1)));

            VerifyNoRecordAdded();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WithAValueOfExactlyTheMaximumLength_WritesIt()
        {
            // Arrange - the boundary belongs to the accepted side
            SetUpZone();
            string longestAllowedValue = new string('a', AcmeChallenge.MaxValueBytes);

            // Act
            ChallengeSetResult result = await _service.SetChallengeRecordAsync(Domain, longestAllowedValue);

            // Assert
            Assert.AreEqual(ChallengeSetResult.Created, result);
            VerifyAdded(ChallengeRecordName, "TXT", longestAllowedValue, AcmeChallenge.RecordTtl);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task SetChallengeRecordAsync_WithBlankValue_ThrowsAndWritesNothing(string? value)
        {
            // Arrange
            SetUpZone();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _service.SetChallengeRecordAsync(Domain, value!));

            VerifyNoRecordAdded();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task SetChallengeRecordAsync_WithBlankDomain_ThrowsAndWritesNothing(string? domain)
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _service.SetChallengeRecordAsync(domain!, ChallengeValue));

            VerifyNoRecordAdded();
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_RemovesEveryChallengeValue()
        {
            // Arrange
            NetlifyDnsRecord firstChallenge = Challenge(OtherChallengeValue);
            NetlifyDnsRecord secondChallenge = Challenge(ChallengeValue);

            SetUpZone(firstChallenge, secondChallenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain);

            // Assert - a stale challenge record left behind is the failure this prevents
            Assert.AreEqual(2, deleted);
            VerifyDeleted(firstChallenge);
            VerifyDeleted(secondChallenge);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_WithAValue_RemovesOnlyThatValue()
        {
            // Arrange
            NetlifyDnsRecord otherChallenge = Challenge(OtherChallengeValue);
            NetlifyDnsRecord targetChallenge = Challenge(ChallengeValue);

            SetUpZone(otherChallenge, targetChallenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain, ChallengeValue);

            // Assert
            Assert.AreEqual(1, deleted);
            VerifyDeleted(targetChallenge);
            VerifyNotDeleted(otherChallenge);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_WithAValueThatIsNotPublished_RemovesNothing()
        {
            // Arrange
            NetlifyDnsRecord otherChallenge = Challenge(OtherChallengeValue);

            SetUpZone(otherChallenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain, ChallengeValue);

            // Assert - the count is what tells a caller its cleanup found nothing
            Assert.AreEqual(0, deleted);
            VerifyNotDeleted(otherChallenge);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_WithAQuotedValueInTheZone_StillRemovesIt()
        {
            // Arrange - a value-scoped cleanup must not silently match nothing because of quoting
            NetlifyDnsRecord quotedChallenge = DnsRecordFactory.Create(ChallengeRecordName, "TXT", $"\"{ChallengeValue}\"", AcmeChallenge.RecordTtl);

            SetUpZone(quotedChallenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain, ChallengeValue);

            // Assert
            Assert.AreEqual(1, deleted);
            VerifyDeleted(quotedChallenge);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_WithTheValueInAnotherCase_RemovesNothing()
        {
            // Arrange - ACME compares the value byte for byte, so case is part of the value
            NetlifyDnsRecord challenge = Challenge(ChallengeValue);

            SetUpZone(challenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain, ChallengeValue.ToUpperInvariant());

            // Assert
            Assert.AreEqual(0, deleted);
            VerifyNotDeleted(challenge);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_WhenTheRecordNameDiffersInCase_StillRemovesIt()
        {
            // Arrange - DNS names are case insensitive, so this is the same record
            NetlifyDnsRecord challenge = DnsRecordFactory.Create(
                ChallengeRecordName.ToUpperInvariant(),
                "TXT",
                ChallengeValue,
                AcmeChallenge.RecordTtl);

            SetUpZone(challenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain);

            // Assert
            Assert.AreEqual(1, deleted);
            VerifyDeleted(challenge);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_LeavesTheDomainsOwnRecordsAlone()
        {
            // Arrange - the zone also holds the records this service must never touch
            NetlifyDnsRecord aRecord = DnsRecordFactory.Create(Domain, "A", "10.1.1.7");
            NetlifyDnsRecord txtAtTheDomain = DnsRecordFactory.Create(Domain, "TXT", "some-unrelated-verification-value");
            NetlifyDnsRecord challengeForAnotherHost = DnsRecordFactory.Create("_acme-challenge.other.example.com", "TXT", ChallengeValue);
            NetlifyDnsRecord delegationAtTheChallengeName = DnsRecordFactory.Create(ChallengeRecordName, "CNAME", "validation.acme-dns.example.net");
            NetlifyDnsRecord challenge = Challenge(ChallengeValue);

            SetUpZone(aRecord, txtAtTheDomain, challengeForAnotherHost, delegationAtTheChallengeName, challenge);

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain);

            // Assert
            Assert.AreEqual(1, deleted);
            VerifyDeleted(challenge);
            VerifyNotDeleted(aRecord);
            VerifyNotDeleted(txtAtTheDomain);
            VerifyNotDeleted(challengeForAnotherHost);
            VerifyNotDeleted(delegationAtTheChallengeName);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_WhenNothingIsPublished_RemovesNothing()
        {
            // Arrange
            SetUpZone(DnsRecordFactory.Create(Domain, "A", "10.1.1.7"));

            // Act
            int deleted = await _service.DeleteChallengeRecordsAsync(Domain);

            // Assert
            Assert.AreEqual(0, deleted);
            VerifyNoRecordDeleted();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task DeleteChallengeRecordsAsync_WithBlankDomain_ThrowsAndRemovesNothing(string? domain)
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _service.DeleteChallengeRecordsAsync(domain!));

            VerifyNoRecordDeleted();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_PassesTheCancellationTokenToEveryProviderCall()
        {
            // Arrange
            using CancellationTokenSource requestAborted = new CancellationTokenSource();

            _netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(Domain, requestAborted.Token))
                .ReturnsAsync(DnsRecordFactory.Zone());

            // Act
            await _service.SetChallengeRecordAsync(Domain, ChallengeValue, requestAborted.Token);

            // Assert - a publish for a client that went away should not run on regardless
            _netlifyServiceMock.Verify(service => service.GetAllDnsRecordsAsync(Domain, requestAborted.Token), Times.Once);
            _netlifyServiceMock.Verify(
                service => service.AddDnsRecordAsync(ChallengeRecordName, Domain, "TXT", ChallengeValue, AcmeChallenge.RecordTtl, requestAborted.Token),
                Times.Once);
        }

        [TestMethod]
        public async Task DeleteChallengeRecordsAsync_PassesTheCancellationTokenToEveryProviderCall()
        {
            // Arrange
            using CancellationTokenSource requestAborted = new CancellationTokenSource();
            NetlifyDnsRecord challenge = Challenge(ChallengeValue);

            _netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(Domain, requestAborted.Token))
                .ReturnsAsync(DnsRecordFactory.Zone(challenge));

            // Act
            await _service.DeleteChallengeRecordsAsync(Domain, value: null, requestAborted.Token);

            // Assert
            _netlifyServiceMock.Verify(service => service.GetAllDnsRecordsAsync(Domain, requestAborted.Token), Times.Once);
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.Is<NetlifyDnsRecord>(deleted => deleted.Id == challenge.Id), requestAborted.Token),
                Times.Once);
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_ConcurrentCallsForOneName_DoNotBothWriteTheSameValue()
        {
            // Arrange
            HeldZone zone = new HeldZone(_netlifyServiceMock, Domain, ChallengeRecordName, AcmeChallenge.RecordType, ChallengeValue, AcmeChallenge.RecordTtl);

            // Act - both calls publish the same value, the second while the first is still reading
            Task<ChallengeSetResult> firstCall = _service.SetChallengeRecordAsync(Domain, ChallengeValue);
            await zone.FirstReadStarted;
            Task<ChallengeSetResult> secondCall = _service.SetChallengeRecordAsync(Domain, ChallengeValue);

            zone.ReleaseFirstRead();
            ChallengeSetResult[] results = await Task.WhenAll(firstCall, secondCall);

            // Assert - the second call saw the first one's record, so only one was written
            Assert.AreEqual(1, results.Count(result => result == ChallengeSetResult.Created));
            Assert.AreEqual(1, results.Count(result => result == ChallengeSetResult.AlreadyPublished));
            VerifyAddedOnce();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_ConcurrentCallsNamingTheDomainInDifferentCases_AreStillSerialized()
        {
            // Arrange - the same record, spelled two ways, must not be written twice
            HeldZone zone = new HeldZone(_netlifyServiceMock, Domain, ChallengeRecordName, AcmeChallenge.RecordType, ChallengeValue, AcmeChallenge.RecordTtl);

            _netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(Domain.ToUpperInvariant(), It.IsAny<CancellationToken>()))
                .Returns(() => zone.ReadAsync());

            // Act
            Task<ChallengeSetResult> firstCall = _service.SetChallengeRecordAsync(Domain, ChallengeValue);
            await zone.FirstReadStarted;
            Task<ChallengeSetResult> secondCall = _service.SetChallengeRecordAsync(Domain.ToUpperInvariant(), ChallengeValue);

            zone.ReleaseFirstRead();
            ChallengeSetResult[] results = await Task.WhenAll(firstCall, secondCall);

            // Assert
            Assert.AreEqual(1, results.Count(result => result == ChallengeSetResult.Created));
            VerifyAddedOnce();
        }

        [TestMethod]
        public async Task SetChallengeRecordAsync_WhenACallerWaitingForTheNameIsCancelled_GivesUpItsPlaceInTheQueue()
        {
            // Arrange - the first call holds the record name while the second one queues behind it
            HeldZone zone = new HeldZone(_netlifyServiceMock, Domain, ChallengeRecordName, AcmeChallenge.RecordType, ChallengeValue, AcmeChallenge.RecordTtl);
            using CancellationTokenSource secondCallAborted = new CancellationTokenSource();

            Task<ChallengeSetResult> firstCall = _service.SetChallengeRecordAsync(Domain, ChallengeValue);
            await zone.FirstReadStarted;

            // Act - the second caller goes away while it is still waiting for the name
            Task<ChallengeSetResult> secondCall = _service.SetChallengeRecordAsync(Domain, OtherChallengeValue, secondCallAborted.Token);
            secondCallAborted.Cancel();

            zone.ReleaseFirstRead();
            await firstCall;

            // Assert - it did not go on to write once the name came free
            try
            {
                await secondCall;
                Assert.Fail("The cancelled caller still took its turn and wrote its value.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static NetlifyDnsRecord Challenge(string value)
        {
            return DnsRecordFactory.Create(ChallengeRecordName, "TXT", value, AcmeChallenge.RecordTtl);
        }

        private void SetUpZone(params NetlifyDnsRecord[] records)
        {
            _netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(Domain, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DnsRecordFactory.Zone(records));
        }

        private void VerifyAdded(string recordName, string type, string value, long ttl)
        {
            _netlifyServiceMock.Verify(
                service => service.AddDnsRecordAsync(recordName, Domain, type, value, ttl, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private void VerifyAddedOnce()
        {
            _netlifyServiceMock.Verify(
                service => service.AddDnsRecordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private void VerifyDeleted(NetlifyDnsRecord record)
        {
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.Is<NetlifyDnsRecord>(deleted => deleted.Id == record.Id), It.IsAny<CancellationToken>()),
                Times.Once,
                $"Expected {record} to be deleted.");
        }

        private void VerifyNotDeleted(NetlifyDnsRecord record)
        {
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.Is<NetlifyDnsRecord>(deleted => deleted.Id == record.Id), It.IsAny<CancellationToken>()),
                Times.Never,
                $"Expected {record} to be left alone.");
        }

        private void VerifyNoRecordAdded()
        {
            _netlifyServiceMock.Verify(
                service => service.AddDnsRecordAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private void VerifyNoRecordDeleted()
        {
            _netlifyServiceMock.Verify(
                service => service.DeleteDnsRecordAsync(It.IsAny<NetlifyDnsRecord>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
