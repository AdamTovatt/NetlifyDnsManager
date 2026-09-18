using NetlifyDnsManager.Services;

namespace NetlifyDnsManager.Tests.TestSupport
{
    /// <summary>
    /// An address source that counts how often a worker read it, so that a test can wait for a cycle
    /// to have happened rather than for a length of time. Without the count, a test asserting that
    /// nothing was published passes just as well when no cycle ran at all.
    /// </summary>
    internal sealed class CountingAddressSource : IIpAddressService
    {
        private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(10);

        private readonly Queue<string> _addresses;
        private readonly string? _addressAfterTheList;
        private readonly Exception? _failure;
        private int _readCount;

        private CountingAddressSource(IEnumerable<string> addresses, Exception? failure)
        {
            _addresses = new Queue<string>(addresses);
            _addressAfterTheList = _addresses.Count > 0 ? _addresses.Last() : null;
            _failure = failure;
        }

        /// <summary>
        /// Creates a source that serves the given addresses in order, and then keeps serving the last
        /// of them, so that a test decides how many cycles matter rather than how many run.
        /// </summary>
        /// <param name="addresses">The addresses to serve, in order.</param>
        /// <returns>The source.</returns>
        public static CountingAddressSource Returning(params string[] addresses)
        {
            return new CountingAddressSource(addresses, failure: null);
        }

        /// <summary>
        /// Creates a source that cannot produce an address, as an address source reports that.
        /// </summary>
        /// <param name="failure">The exception every read throws.</param>
        /// <returns>The source.</returns>
        public static CountingAddressSource Failing(Exception failure)
        {
            return new CountingAddressSource(Enumerable.Empty<string>(), failure);
        }

        /// <summary>
        /// Gets how many times the address has been read.
        /// </summary>
        public int ReadCount => Volatile.Read(ref _readCount);

        public string Description => "counting test source";

        public Task<string> GetIpAddressAsync()
        {
            Interlocked.Increment(ref _readCount);

            if (_failure != null)
                return Task.FromException<string>(_failure);

            lock (_addresses)
            {
                return Task.FromResult(_addresses.Count > 0 ? _addresses.Dequeue() : _addressAfterTheList!);
            }
        }

        /// <summary>
        /// Waits until the address has been read at least this many times.
        /// </summary>
        /// <param name="readCount">The number of reads to wait for.</param>
        /// <param name="timeout">How long to wait at most.</param>
        /// <returns>True when that many reads happened, false on timeout.</returns>
        public Task<bool> WaitForReadsAsync(int readCount, TimeSpan? timeout = null)
        {
            return TestWait.UntilAsync(() => ReadCount >= readCount, timeout ?? DefaultWaitTimeout);
        }
    }
}
