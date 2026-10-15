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
            int throttled = 0;

            while (true)
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
                        trace.Debug("Request body: " + Clip(body is string ? Json.Redact(json) : Json.Serialize(body, redactSecrets: true)));
                    }

                    using (var response = HttpTransport.Send(request, cancel))
                    {
                        string text = HttpTransport.ReadBody(response);
                        trace?.Verbose?.Invoke($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                        trace?.Debug?.Invoke("Response body: " + Clip(Json.Redact(text)));

                        if (response.StatusCode == HttpStatusCode.Unauthorized && !renewed && IsTokenRejection(text) &&
                            session.Tokens.RenewRejected(token, cancel))
                        {
                            // The token may have been revoked or expired early; renew it once and try again.
                            renewed = true;
                            continue;
                        }

                        if ((int)response.StatusCode == 429 && throttled < MaxThrottleRetries)
                        {
                            var wait = RetryAfter(response) ?? TimeSpan.FromSeconds(Math.Pow(2, throttled + 1));
                            if (wait <= MaxThrottleWait)
                            {
                                throttled++;
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

        /// <summary>The wait a 429 asks for, whether Retry-After gives seconds or a date.</summary>
        private static TimeSpan? RetryAfter(HttpResponseMessage response)
        {
            var header = response.Headers.RetryAfter;
            if (header?.Delta != null) return header.Delta;
            if (header?.Date != null)
            {
                var wait = header.Date.Value - DateTimeOffset.UtcNow;
                return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
            }
            return null;
        }

        /// <summary>
        /// Whether a 401 says the access token itself is bad, so renewing it can help. A 401 about
        /// permissions (a scope, a customer, a plan) would fail the same way with a new token, and every
        /// needless renewal counts against Zoho's limit of about ten per ten minutes.
        /// The codes are the ones Site24x7 uses for invalid and expired OAuth tokens; any 401 that names
        /// the token, or carries no Site24x7 code at all, is treated the same way.
        /// </summary>
        internal static bool IsTokenRejection(string body)
        {
            object parsed;
            try
            {
                parsed = Json.Parse(body);
            }
            catch (FormatException)
            {
                return true;
            }

            var code = Json.Get(parsed, "code");
            if (code == null) return true;
            var codeText = Convert.ToString(code, CultureInfo.InvariantCulture);
            if (codeText == "1100" || codeText == "1101" || codeText == "1102") return true;

            var message = Convert.ToString(Json.Get(parsed, "message"), CultureInfo.InvariantCulture) ?? string.Empty;
            return message.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("oauth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Debug output is written whether or not anyone is listening (cmdlets cannot see the caller's
        // $DebugPreference), so long bodies are cut to keep that cheap.
        private const int MaxTracedBody = 16384;

        private static string Clip(string text) =>
            text.Length <= MaxTracedBody ? text : text.Substring(0, MaxTracedBody) + $"... ({text.Length - MaxTracedBody} more characters)";

        /// <summary>
        /// Resolves <paramref name="path"/> against the API root. Only a path relative to the root is
        /// accepted, and the result must still be inside the root on the same host, so the access token
        /// is never sent anywhere else.
        /// </summary>
        internal static Uri BuildUri(Uri baseUri, string path, IDictionary query)
        {
            path = path ?? string.Empty;
            foreach (char c in path)
            {
                if (c == '\\' || char.IsControl(c))
                {
                    throw new Site24x7ArgumentException($"The endpoint '{path}' contains a backslash or a control character. Use a path relative to the API root, such as 'monitors/123'.");
                }
            }
            if (path.Length > 0 && (char.IsWhiteSpace(path[0]) || char.IsWhiteSpace(path[path.Length - 1])))
            {
                throw new Site24x7ArgumentException($"The endpoint '{path}' starts or ends with a space. Use a path relative to the API root, such as 'monitors/123'.");
            }
            if (path.IndexOf("://", StringComparison.Ordinal) >= 0 || path.StartsWith("//", StringComparison.Ordinal))
            {
                throw new Site24x7ArgumentException($"'{path}' is a full URL. Pass a path relative to the API root ({baseUri}), such as 'monitors/123'.");
            }

            var relative = path.TrimStart('/');
            if (relative.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(4);
            }

            var resolved = new Uri(baseUri, relative);
            if (!resolved.IsAbsoluteUri || resolved.IsUnc ||
                Uri.Compare(resolved, baseUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0 ||
                !resolved.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new Site24x7ArgumentException($"The endpoint '{path}' resolves outside the API root ({baseUri}). Use a path relative to it, such as 'monitors/123'.");
            }
            return AppendQuery(resolved, query);
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
                case IDictionary _:
                    throw new Site24x7ArgumentException("A query value cannot be a hashtable or object. Pass a string, number, date or list.");
                case PSCustomObject _:
                    throw new Site24x7ArgumentException("A query value cannot be a hashtable or object. Pass a string, number, date or list.");
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
                if (raw) return text;

                // A login page, captive portal or wrong -ApiBaseUri answers 200 with HTML; that is not data.
                var contentType = response.Content?.Headers?.ContentType?.MediaType ?? "an unknown content type";
                throw new Site24x7ApiException(
                    $"Site24x7 answered HTTP {(int)response.StatusCode} with {contentType} instead of JSON. Check -ApiBaseUri and any proxy, or use Invoke-Site24x7Api -Raw to read the text.",
                    response.StatusCode, null, null, text);
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
