using System.Text;

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
        /// The longest a single DNS TXT string can be, counted the way DNS counts it: in bytes, because
        /// the string is stored behind a single length byte.
        /// </summary>
        public const int MaxValueBytes = 255;

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
        /// Whether a challenge value is too long to publish as one TXT string. The measurement is in
        /// UTF-8 bytes rather than characters, because that is the limit DNS imposes and a character
        /// can encode to as many as four of them.
        /// </summary>
        /// <param name="value">The challenge value.</param>
        /// <returns>True when the value does not fit in one TXT string.</returns>
        public static bool IsValueTooLong(string value)
        {
            return Encoding.UTF8.GetByteCount(value) > MaxValueBytes;
        }
    }
}
