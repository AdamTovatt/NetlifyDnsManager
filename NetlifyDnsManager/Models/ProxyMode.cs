namespace NetlifyDnsManager.Models
{
    /// <summary>
    /// Defines the operating mode for the Netlify DNS Manager.
    /// </summary>
    public enum ProxyMode
    {
        /// <summary>
        /// Default mode. Checks own public IP and updates Netlify DNS directly.
        /// </summary>
        None,

        /// <summary>
        /// Server mode. Does everything <see cref="None"/> does, plus runs a web API
        /// that accepts DNS update requests from authenticated clients.
        /// </summary>
        Server,

        /// <summary>
        /// Client mode. Checks own public IP and sends it to a remote server
        /// instead of updating Netlify directly.
        /// </summary>
        Client
    }
}
