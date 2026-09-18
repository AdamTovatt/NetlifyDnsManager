namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// The outcome of publishing a challenge value.
    /// </summary>
    public enum ChallengeSetResult
    {
        /// <summary>
        /// The value was published.
        /// </summary>
        Created,

        /// <summary>
        /// The value was already published, so nothing changed.
        /// </summary>
        AlreadyPublished,

        /// <summary>
        /// The record name already holds <see cref="AcmeChallenge.MaxValuesPerName"/> values, which
        /// means earlier challenges were never cleaned up. Nothing was published.
        /// </summary>
        TooManyValues
    }
}
