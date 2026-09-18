namespace NetlifyDnsManager.Tests.TestSupport
{
    /// <summary>
    /// Categories used to select which tests to run.
    /// </summary>
    public static class TestCategories
    {
        /// <summary>
        /// Tests that call live external services, so they need credentials and network access
        /// and are excluded in CI. Every test without this category runs there.
        /// </summary>
        public const string Integration = "Integration";
    }
}
