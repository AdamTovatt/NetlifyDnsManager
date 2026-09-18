using NetlifyDnsManager.Models;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Builds the DNS records a zone returns, so tests name only what they are about.
    /// </summary>
    internal static class DnsRecordFactory
    {
        /// <summary>
        /// Creates a DNS record. The id is derived from the record itself, so a test can say which
        /// record it expected to be deleted without holding on to a generated value.
        /// </summary>
        /// <param name="hostname">The record's host name.</param>
        /// <param name="type">The record type.</param>
        /// <param name="value">The record value.</param>
        /// <param name="ttl">The record's time to live.</param>
        /// <returns>The record.</returns>
        public static NetlifyDnsRecord Create(string hostname, string type, string value, long ttl = 1800)
        {
            return new NetlifyDnsRecord(
                hostname: hostname,
                type: type,
                ttl: ttl,
                priority: null,
                weight: null,
                port: null,
                flag: null,
                tag: null,
                id: $"{type}:{hostname}:{value}",
                siteId: null,
                dnsZoneId: "test_zone",
                errors: new List<object>(),
                managed: false,
                value: value);
        }

        /// <summary>
        /// Creates a zone holding the given records.
        /// </summary>
        /// <param name="records">The records in the zone.</param>
        /// <returns>The zone's records.</returns>
        public static NetlifyDnsRecords Zone(params NetlifyDnsRecord[] records)
        {
            return new NetlifyDnsRecords(records.ToList());
        }
    }
}
