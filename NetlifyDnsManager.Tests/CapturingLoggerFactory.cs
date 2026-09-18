using Microsoft.Extensions.Logging;

namespace NetlifyDnsManager.Tests
{
    /// <summary>
    /// Records what an endpoint logged, and under which category, so that a test can check what an
    /// operator would be able to see and filter on.
    /// </summary>
    internal sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly List<string> _categories = new List<string>();
        private readonly List<string> _messages = new List<string>();

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
                lock (_messages)
                {
                    return _messages.ToList();
                }
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

        private void Record(string message)
        {
            lock (_messages)
            {
                _messages.Add(message);
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
                _factory.Record(formatter(state, exception));
            }
        }
    }
}
