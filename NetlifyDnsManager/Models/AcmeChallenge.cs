namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// What an ACME DNS-01 challenge record is: where it sits, what it may hold, and how many of
    /// them one name may carry. The single home for these rules, so the endpoint that validates a
    /// request and the service that writes the record cannot disagree.
    /// </summary>
    public static class AcmeChallenge
    {
        /// <summary>
        /// The label ACME requires the challenge record to sit at, below the validated name.
        /// </summary>
        public const string RecordLabel = "_acme-challenge";

        /// <summary>
        /// The record type a DNS-01 challenge is published as.
        /// </summary>
        public const string RecordType = "TXT";

        /// <summary>
        /// A short time to live, because a challenge record lives for one validation and any stale
        /// copy of it should expire quickly.
        /// </summary>
        public const long RecordTtl = 60;

        /// <summary>
        /// The longest a single DNS TXT string can be.
        /// </summary>
        public const int MaxValueLength = 255;

        /// <summary>
        /// The most values one challenge record name may hold. A certificate covering a name and its
        /// wildcard needs two at the same name; the rest is room for one interrupted run's leftovers,
        /// so that a client cannot grow the shared zone without bound.
        /// </summary>
        public const int MaxValuesPerName = 4;

        /// <summary>
        /// Gets the challenge record name for a domain.
        /// </summary>
        /// <param name="domain">The domain being validated.</param>
        /// <returns>The challenge record name, for example "_acme-challenge.example.com".</returns>
        public static string RecordNameFor(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be null or empty.", nameof(domain));

            return $"{RecordLabel}.{domain}";
        }

        /// <summary>
        /// Reads a TXT record value as the string it publishes, without the quoting a DNS provider
        /// may add around it. Comparing raw values would silently match nothing if the value came
        /// back quoted, which would leave a challenge record behind while reporting success.
        /// </summary>
        /// <param name="recordValue">The value as the provider returned it.</param>
        /// <returns>The published string.</returns>
        public static string ReadValue(string recordValue)
        {
            if (recordValue.Length >= 2 && recordValue.StartsWith('"') && recordValue.EndsWith('"'))
            {
                return recordValue.Substring(1, recordValue.Length - 2);
            }

            return recordValue;
        }
    }
}
