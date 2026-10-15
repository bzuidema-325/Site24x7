using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Site24x7
{
    /// <summary>One shared HttpClient for the module, as recommended for long-lived processes.</summary>
    internal static class HttpTransport
    {
        private static readonly Lazy<HttpClient> LazyClient = new Lazy<HttpClient>(Create, LazyThreadSafetyMode.ExecutionAndPublication);

        public static HttpClient Client => LazyClient.Value;

        private static HttpClient Create()
        {
            // Windows PowerShell 5.1 on older .NET Framework builds does not offer TLS 1.2 by default.
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
                // Not settable on this runtime; the OS default is used.
            }

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
            };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Site24x7-PowerShell/2.0");
            return client;
        }

        public static HttpResponseMessage Send(HttpRequestMessage request)
        {
            return Client.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static string ReadBody(HttpResponseMessage response)
        {
            return response.Content == null
                ? string.Empty
                : response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
