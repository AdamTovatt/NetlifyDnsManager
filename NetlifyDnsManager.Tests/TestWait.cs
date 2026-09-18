namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Waits for a condition instead of for a fixed amount of time, so a passing test finishes
    /// as soon as the work is done and a failing one still gives up.
    /// </summary>
    internal static class TestWait
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// Waits until the condition holds or the timeout elapses.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">How long to wait at most.</param>
        /// <returns>True if the condition held before the timeout, false otherwise.</returns>
        public static async Task<bool> UntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout);

            while (!condition())
            {
                if (timeoutSource.IsCancellationRequested)
                {
                    return condition();
                }

                await Task.Delay(PollInterval);
            }

            return true;
        }
    }
}
