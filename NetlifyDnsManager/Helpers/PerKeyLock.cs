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
        /// Runs the given work, waiting for any work already running for the same key.
        /// </summary>
        /// <typeparam name="T">The type the work returns.</typeparam>
        /// <param name="key">The key to serialize on.</param>
        /// <param name="work">The work to run.</param>
        /// <returns>What the work returned.</returns>
        public async Task<T> RunAsync<T>(string key, Func<Task<T>> work)
        {
            SemaphoreSlim keyLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync();

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
