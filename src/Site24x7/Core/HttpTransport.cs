using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Site24x7
{
    /// <summary>One shared HttpClient for the module, as recommended for long-lived processes.</summary>
    internal static class HttpTransport
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

        private static readonly Lazy<HttpClient> LazyClient = new Lazy<HttpClient>(Create, LazyThreadSafetyMode.ExecutionAndPublication);

        public static HttpClient Client => LazyClient.Value;

        private static HttpClient Create()
        {
            EnableTls12();

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
            };
            var client = new HttpClient(handler) { Timeout = Timeout };
            var version = typeof(HttpTransport).Assembly.GetName().Version;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"Site24x7-PowerShell/{version.Major}.{version.Minor}.{version.Build}");
            return client;
        }

        /// <summary>
        /// Old .NET Framework builds default to SSL 3 and TLS 1.0, which Site24x7 refuses. Newer ones
        /// default to SystemDefault (0), which lets the OS pick, including TLS 1.3; that is left alone,
        /// because OR-ing in Tls12 would pin the whole process to TLS 1.2.
        /// </summary>
        private static void EnableTls12()
        {
            try
            {
                var current = ServicePointManager.SecurityProtocol;
                if (current != 0 && (current & SecurityProtocolType.Tls12) == 0)
                {
                    ServicePointManager.SecurityProtocol = current | SecurityProtocolType.Tls12;
                }
            }
            catch (NotSupportedException)
            {
                // Not settable on this runtime; the OS default is used.
            }
        }

        /// <summary>
        /// Sends a request and buffers the response. Cancelling <paramref name="cancel"/> (Ctrl+C)
        /// abandons the request at once; a request that runs past the timeout throws TimeoutException.
        /// </summary>
        public static HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancel)
        {
            try
            {
                return Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancel).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException ex) when (!cancel.IsCancellationRequested)
            {
                throw new TimeoutException($"{request.RequestUri.Host} did not answer within {Timeout.TotalSeconds:0} seconds.", ex);
            }
        }

        /// <summary>Reads the already-buffered response body.</summary>
        public static string ReadBody(HttpResponseMessage response)
        {
            return response.Content == null
                ? string.Empty
                : response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
