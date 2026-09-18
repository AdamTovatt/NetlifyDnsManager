using System.Collections.Concurrent;

namespace NetlifyDnsManager.Helpers
{
    /// <summary>
    /// Serializes work per key, so two operations on the same key never interleave and read each
    /// other's half-finished state. Keys are DNS names, which are case insensitive, so two spellings
    /// of one name share a lock rather than getting one each.
    /// </summary>
    internal sealed class PerKeyLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Runs the given work, waiting for any work already running for the same key. The wait is
        /// given the token as well, so that a caller which is already gone gives up its place in the
        /// queue instead of holding the key against the callers still waiting behind it.
        /// </summary>
        /// <typeparam name="T">The type the work returns.</typeparam>
        /// <param name="key">The key to serialize on.</param>
        /// <param name="cancellationToken">Token that abandons the wait for the key.</param>
        /// <param name="work">The work to run.</param>
        /// <returns>What the work returned.</returns>
        public async Task<T> RunAsync<T>(string key, CancellationToken cancellationToken, Func<Task<T>> work)
        {
            SemaphoreSlim keyLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync(cancellationToken);

            try
            {
                return await work();
            }
            finally
            {
                keyLock.Release();
            }
        }
    }
}
