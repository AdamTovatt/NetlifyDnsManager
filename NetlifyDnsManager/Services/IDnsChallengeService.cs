using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Services
{
    /// <summary>
    /// Service interface for managing the TXT records an ACME DNS-01 challenge needs. The record name
    /// always comes from <see cref="AcmeChallenge.RecordNameFor"/>, never from a caller, which is what
    /// keeps a client to the domains its key authorizes.
    /// </summary>
    public interface IDnsChallengeService
    {
        /// <summary>
        /// Adds a challenge value for a domain, leaving any other values at the same record name in
        /// place so that two challenges for one name can be answered at the same time.
        /// </summary>
        /// <param name="domain">The domain being validated.</param>
        /// <param name="value">The challenge value to publish.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>What happened: the value was published, was already there, or the name holds too many values.</returns>
        Task<ChallengeSetResult> SetChallengeRecordAsync(string domain, string value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes challenge records for a domain. Only TXT records at the challenge record name are
        /// ever removed, so the domain's own records are untouched.
        /// </summary>
        /// <param name="domain">The domain being validated.</param>
        /// <param name="value">The single value to remove, or null to remove every challenge value.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>The number of records removed.</returns>
        Task<int> DeleteChallengeRecordsAsync(string domain, string? value = null, CancellationToken cancellationToken = default);
    }
}
