using Moq;
using NetlifyDnsManager.Models;
using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests.TestSupport
{
    /// <summary>
    /// A zone whose first read is held open, so that a second call can be made to arrive in the middle
    /// of the first one. The zone is read as it is when a call starts, so a call that is not made to
    /// wait for the one before it sees a zone without that call's record, and writes a second one.
    /// This is what tells a lock that works from one that does not.
    /// </summary>
    internal sealed class HeldZone
    {
        private readonly TaskCompletionSource _firstReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<NetlifyDnsRecord> _records = new List<NetlifyDnsRecord>();
        private readonly string _recordName;
        private readonly string _recordType;
        private readonly string _value;
        private readonly long _ttl;
        private int _readCount;

        /// <summary>
        /// Sets a Netlify service up to read this zone and to add the record described here to it.
        /// </summary>
        /// <param name="netlifyServiceMock">The mock to set up.</param>
        /// <param name="domain">The domain whose zone is read.</param>
        /// <param name="recordName">The name of the record a call writes.</param>
        /// <param name="recordType">The type of the record a call writes.</param>
        /// <param name="value">The value a call writes.</param>
        /// <param name="ttl">The time to live a call writes.</param>
        public HeldZone(Mock<INetlifyService> netlifyServiceMock, string domain, string recordName, string recordType, string value, long ttl)
        {
            _recordName = recordName;
            _recordType = recordType;
            _value = value;
            _ttl = ttl;

            netlifyServiceMock.Setup(service => service.GetAllDnsRecordsAsync(domain, It.IsAny<CancellationToken>()))
                .Returns(() => ReadAsync());

            netlifyServiceMock.Setup(service => service.AddDnsRecordAsync(
                    recordName, It.IsAny<string>(), recordType, value, ttl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Add);
        }

        /// <summary>
        /// Completes once a first call has reached the zone read and is being held there.
        /// </summary>
        public Task FirstReadStarted => _firstReadStarted.Task;

        /// <summary>
        /// Lets the held first read finish.
        /// </summary>
        public void ReleaseFirstRead() => _releaseFirstRead.TrySetResult();

        /// <summary>
        /// Reads the zone as it is at this moment, holding the first such read open.
        /// </summary>
        /// <returns>The records in the zone when the read started.</returns>
        public async Task<NetlifyDnsRecords> ReadAsync()
        {
            List<NetlifyDnsRecord> zoneAtReadTime;

            lock (_records)
            {
                zoneAtReadTime = _records.ToList();
            }

            if (Interlocked.Increment(ref _readCount) == 1)
            {
                _firstReadStarted.TrySetResult();
                await _releaseFirstRead.Task;
            }

            return new NetlifyDnsRecords(zoneAtReadTime);
        }

        private NetlifyDnsRecord Add()
        {
            NetlifyDnsRecord added = DnsRecordFactory.Create(_recordName, _recordType, _value, _ttl);

            lock (_records)
            {
                _records.Add(added);
            }

            return added;
        }
    }
}
