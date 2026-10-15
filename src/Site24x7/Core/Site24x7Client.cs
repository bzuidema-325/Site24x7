using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace Site24x7
{
    /// <summary>Where the client reports what it is doing: request lines to Verbose, bodies to Debug.</summary>
    internal sealed class RequestTrace
    {
        public Action<string> Verbose;
        public Action<string> Debug;
    }

    /// <summary>
    /// Sends requests to the Site24x7 REST API and unwraps the standard
    /// {"code":0,"message":"success","data":...} envelope.
    /// https://www.site24x7.com/help/api/#response
    /// </summary>
    internal static class Site24x7Client
    {
        public const string DefaultApiVersion = "2.0";

        private const int MaxThrottleRetries = 3;

        /// <summary>The longest Retry-After the client waits out; a longer one is reported as an error.</summary>
        private static readonly TimeSpan MaxThrottleWait = TimeSpan.FromSeconds(60);

        public static object Invoke(
            Site24x7Session session,
            string method,
            string path,
            IDictionary query,
            object body,
            string apiVersion,
            bool raw,
            RequestTrace trace,
            CancellationToken cancel)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var uri = BuildUri(session.ApiBaseUri, path, query);
            string json = body == null ? null : (body as string ?? Json.Serialize(body));
            bool renewed = false;

            for (int attempt = 0; ; attempt++)
            {
                string token = session.Tokens.GetAccessToken(cancel);
                using (var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", token);
                    request.Headers.Accept.ParseAdd("application/json; version=" + (apiVersion ?? DefaultApiVersion));
                    if (session.CustomerId != null)
                    {
                        request.Headers.Add("Cookie", "zaaid=" + Secrets.CheckCustomerId(session.CustomerId));
                    }
                    if (json != null)
                    {
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }

                    trace?.Verbose?.Invoke($"{request.Method} {uri}");
                    if (json != null && trace?.Debug != null)
                    {
                        trace.Debug("Request body: " + Clip(body is string ? json : Json.Serialize(body, redactSecrets: true)));
                    }

                    using (var response = HttpTransport.Send(request, cancel))
                    {
                        string text = HttpTransport.ReadBody(response);
                        trace?.Verbose?.Invoke($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                        trace?.Debug?.Invoke("Response body: " + Clip(text));

                        if (response.StatusCode == HttpStatusCode.Unauthorized && !renewed && session.Tokens.RenewRejected(token, cancel))
                        {
                            // The token may have been revoked or expired early; renew it once and try again.
                            renewed = true;
                            continue;
                        }

                        if ((int)response.StatusCode == 429 && attempt < MaxThrottleRetries)
                        {
                            var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                            if (wait <= MaxThrottleWait)
                            {
                                trace?.Verbose?.Invoke($"Throttled by Site24x7, retrying in {wait.TotalSeconds:0} seconds.");
                                if (cancel.WaitHandle.WaitOne(wait)) cancel.ThrowIfCancellationRequested();
                                continue;
                            }
                        }

                        return HandleResponse(response, text, raw);
                    }
                }
            }
        }

        // Debug output is written whether or not anyone is listening (cmdlets cannot see the caller's
        // $DebugPreference), so long bodies are cut to keep that cheap.
        private const int MaxTracedBody = 16384;

        private static string Clip(string text) =>
            text.Length <= MaxTracedBody ? text : text.Substring(0, MaxTracedBody) + $"... ({text.Length - MaxTracedBody} more characters)";

        /// <summary>
        /// Resolves <paramref name="path"/> against the API root. An absolute URL is accepted only
        /// when it points inside the session's own API root, so the access token never goes elsewhere.
        /// </summary>
        internal static Uri BuildUri(Uri baseUri, string path, IDictionary query)
        {
            path = path ?? string.Empty;
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out var absolute) ||
                    Uri.Compare(absolute, baseUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0 ||
                    !absolute.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Site24x7ArgumentException($"'{path}' is outside this session's API root ({baseUri}). Pass a path relative to it, such as 'monitors'.");
                }
                return AppendQuery(absolute, query);
            }

            var relative = path.TrimStart('/');
            if (relative.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(4);
            }
            return AppendQuery(new Uri(baseUri, relative), query);
        }

        private static Uri AppendQuery(Uri uri, IDictionary query)
        {
            if (query == null || query.Count == 0) return uri;

            var sb = new StringBuilder();
            foreach (DictionaryEntry entry in query)
            {
                var value = entry.Value is PSObject pso ? pso.BaseObject : entry.Value;
                if (value == null) continue;
                sb.Append(sb.Length == 0 ? string.Empty : "&")
                  .Append(Uri.EscapeDataString(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)))
                  .Append('=')
                  .Append(Uri.EscapeDataString(FormatQueryValue(value)));
            }

            if (sb.Length == 0) return uri;
            var builder = new UriBuilder(uri);
            builder.Query = string.IsNullOrEmpty(builder.Query) || builder.Query == "?"
                ? sb.ToString()
                : builder.Query.TrimStart('?') + "&" + sb;
            return builder.Uri;
        }

        private static string FormatQueryValue(object value)
        {
            switch (value)
            {
                case bool b:
                    return b ? "true" : "false";
                case Enum e:
                    return Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case DateTime dt:
                    return dt.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);
                case string s:
                    return s;
                case IEnumerable list:
                    return string.Join(",", list.Cast<object>().Select(FormatQueryValue));
                case PSObject pso:
                    return FormatQueryValue(pso.BaseObject);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static object HandleResponse(HttpResponseMessage response, string text, bool raw)
        {
            object parsed;
            try
            {
                parsed = Json.Parse(text);
            }
            catch (FormatException)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Site24x7ApiException(
                        $"Site24x7 returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                        response.StatusCode, null, null, text);
                }
                return text;
            }

            // "code" should be a number, but anything else is reported as an API error rather than crashing.
            var codeValue = Json.Get(parsed, "code");
            string codeText = codeValue == null ? null : Convert.ToString(codeValue, CultureInfo.InvariantCulture);
            int? code = int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : (int?)null;

            if (!response.IsSuccessStatusCode || (codeText != null && code != 0))
            {
                var message = Json.Get(parsed, "message") as string;
                var errorInfo = Json.Get(parsed, "error_info");
                var detail = string.IsNullOrEmpty(message) ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" : message;
                if (errorInfo != null)
                {
                    detail += " " + Json.Serialize(errorInfo);
                }
                throw new Site24x7ApiException(
                    $"Site24x7 API error{(codeText != null ? " " + codeText : string.Empty)}: {detail}",
                    response.StatusCode, code, errorInfo, text);
            }

            if (raw) return parsed;

            // Successful responses wrap the payload in "data". Some (such as deletes) carry only code and message.
            if (parsed is PSObject obj && obj.Properties["code"] != null)
            {
                return obj.Properties["data"]?.Value;
            }
            return parsed;
        }
    }
}
