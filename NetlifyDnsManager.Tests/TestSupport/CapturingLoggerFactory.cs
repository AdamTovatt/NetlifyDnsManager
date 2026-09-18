using Microsoft.Extensions.Logging;

namespace NetlifyDnsManager.Tests.TestSupport
{
    /// <summary>
    /// Records what an endpoint logged, and under which category, so that a test can check what an
    /// operator would be able to see and filter on.
    /// </summary>
    internal sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly List<string> _categories = new List<string>();
        private readonly List<(LogLevel Level, string Message)> _entries = new List<(LogLevel, string)>();

        /// <summary>
        /// Gets the categories loggers were created for.
        /// </summary>
        public IReadOnlyList<string> Categories
        {
            get
            {
                lock (_categories)
                {
                    return _categories.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the messages that were logged, formatted.
        /// </summary>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_entries)
                {
                    return _entries.Select(entry => entry.Message).ToList();
                }
            }
        }

        /// <summary>
        /// Gets the messages that were logged at the given level, which is what an operator running
        /// with logging turned down does and does not get to see.
        /// </summary>
        /// <param name="level">The level to read.</param>
        /// <returns>The messages logged at that level.</returns>
        public IReadOnlyList<string> MessagesAt(LogLevel level)
        {
            lock (_entries)
            {
                return _entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            lock (_categories)
            {
                _categories.Add(categoryName);
            }

            return new CapturingLogger(this);
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private void Record(LogLevel level, string message)
        {
            lock (_entries)
            {
                _entries.Add((level, message));
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerFactory _factory;

            public CapturingLogger(CapturingLoggerFactory factory)
            {
                _factory = factory;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _factory.Record(logLevel, formatter(state, exception));
            }
        }
    }
}
