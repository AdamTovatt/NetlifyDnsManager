using NetlifyDnsManager.Helpers;
using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Manages the TXT records an ACME DNS-01 challenge needs, on behalf of clients that hold no
    /// Netlify token of their own.
    /// </summary>
    public class DnsChallengeService : IDnsChallengeService
    {
        private readonly PerKeyLock _recordLocks = new PerKeyLock();
        private readonly INetlifyService _netlifyService;
        private readonly ILogger<DnsChallengeService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsChallengeService"/> class.
        /// </summary>
        /// <param name="netlifyService">The Netlify service for managing DNS records.</param>
        /// <param name="logger">The logger instance.</param>
        public DnsChallengeService(INetlifyService netlifyService, ILogger<DnsChallengeService> logger)
        {
            _netlifyService = netlifyService ?? throw new ArgumentNullException(nameof(netlifyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Adds a challenge value for a domain, leaving any other values at the same record name in place.
        /// </summary>
        /// <param name="domain">The domain being validated.</param>
        /// <param name="value">The challenge value to publish.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>What happened: the value was published, was already there, or the name holds too many values.</returns>
        public Task<ChallengeSetResult> SetChallengeRecordAsync(string domain, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Challenge value cannot be null or empty.", nameof(value));

            if (value.Length > AcmeChallenge.MaxValueLength)
                throw new ArgumentException($"Challenge value cannot be longer than {AcmeChallenge.MaxValueLength} characters.", nameof(value));

            string recordName = AcmeChallenge.RecordNameFor(domain);

            return _recordLocks.RunAsync(recordName, () => SetChallengeRecordInternalAsync(domain, recordName, value, cancellationToken));
        }

        /// <summary>
        /// Removes challenge records for a domain.
        /// </summary>
        /// <param name="domain">The domain being validated.</param>
        /// <param name="value">The single value to remove, or null to remove every challenge value.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>The number of records removed.</returns>
        public Task<int> DeleteChallengeRecordsAsync(string domain, string? value = null, CancellationToken cancellationToken = default)
        {
            string recordName = AcmeChallenge.RecordNameFor(domain);

            return _recordLocks.RunAsync(recordName, () => DeleteChallengeRecordsInternalAsync(domain, recordName, value, cancellationToken));
        }

        private async Task<ChallengeSetResult> SetChallengeRecordInternalAsync(string domain, string recordName, string value, CancellationToken cancellationToken)
        {
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(domain, cancellationToken);
            List<NetlifyDnsRecord> publishedRecords = FindChallengeRecords(allRecords, recordName, value: null);

            if (publishedRecords.Any(record => AcmeChallenge.ReadValue(record.Value) == value))
            {
                _logger.LogInformation("Challenge record {RecordName} already publishes this value", recordName);

                return ChallengeSetResult.AlreadyPublished;
            }

            if (publishedRecords.Count >= AcmeChallenge.MaxValuesPerName)
            {
                _logger.LogWarning(
                    "Challenge record {RecordName} already holds {PublishedCount} values, which means earlier challenges were not removed",
                    recordName,
                    publishedRecords.Count);

                return ChallengeSetResult.TooManyValues;
            }

            await _netlifyService.AddDnsRecordAsync(recordName, domain, AcmeChallenge.RecordType, value, AcmeChallenge.RecordTtl, cancellationToken);

            _logger.LogInformation("Created challenge record {RecordName}", recordName);

            return ChallengeSetResult.Created;
        }

        private async Task<int> DeleteChallengeRecordsInternalAsync(string domain, string recordName, string? value, CancellationToken cancellationToken)
        {
            NetlifyDnsRecords allRecords = await _netlifyService.GetAllDnsRecordsAsync(domain, cancellationToken);
            List<NetlifyDnsRecord> recordsToDelete = FindChallengeRecords(allRecords, recordName, value);

            foreach (NetlifyDnsRecord record in recordsToDelete)
            {
                await _netlifyService.DeleteDnsRecordAsync(record, cancellationToken);
            }

            _logger.LogInformation("Removed {DeletedCount} challenge record(s) at {RecordName}", recordsToDelete.Count, recordName);

            return recordsToDelete.Count;
        }

        /// <summary>
        /// Finds the challenge records at a record name, optionally narrowed to one value. Only TXT
        /// records exactly at the challenge name match, so no other record can be returned for deletion.
        /// Host names are matched without regard to case, because DNS names are case insensitive,
        /// while a challenge value is matched exactly, because ACME compares it byte for byte.
        /// </summary>
        /// <param name="allRecords">Every record in the zone.</param>
        /// <param name="recordName">The challenge record name.</param>
        /// <param name="value">The value to match, or null to match every value.</param>
        /// <returns>The matching records.</returns>
        private static List<NetlifyDnsRecord> FindChallengeRecords(NetlifyDnsRecords allRecords, string recordName, string? value)
        {
            return allRecords.Records
                .Where(record => record.Type == AcmeChallenge.RecordType
                    && string.Equals(record.Hostname, recordName, StringComparison.OrdinalIgnoreCase)
                    && (value == null || AcmeChallenge.ReadValue(record.Value) == value))
                .ToList();
        }
    }
}
