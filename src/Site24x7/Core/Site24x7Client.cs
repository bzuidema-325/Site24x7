using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace Site24x7
{
    /// <summary>
    /// Sends requests to the Site24x7 REST API and unwraps the standard
    /// {"code":0,"message":"success","data":...} envelope.
    /// https://www.site24x7.com/help/api/#response
    /// </summary>
    public static class Site24x7Client
    {
        public const string DefaultApiVersion = "2.0";

        private const int MaxThrottleRetries = 3;

        /// <summary>Optional hook so cmdlets can write the request and response to the verbose and debug streams.</summary>
        public delegate void TraceHandler(string message, bool isDebug);

        public static object Invoke(
            Site24x7Session session,
            string method,
            string path,
            IDictionary query = null,
            object body = null,
            string apiVersion = null,
            bool raw = false,
            TraceHandler trace = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var uri = BuildUri(session.ApiBaseUri, path, query);
            string json = body == null ? null : (body as string ?? Json.Serialize(body));
            bool refreshed = false;

            for (int attempt = 0; ; attempt++)
            {
                string token = session.GetAccessToken();
                using (var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", token);
                    request.Headers.Accept.ParseAdd("application/json; version=" + (apiVersion ?? DefaultApiVersion));
                    if (!string.IsNullOrEmpty(session.CustomerId))
                    {
                        request.Headers.Add("Cookie", "zaaid=" + session.CustomerId);
                    }
                    if (json != null)
                    {
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }

                    trace?.Invoke($"{request.Method} {uri}", false);
                    if (json != null) trace?.Invoke("Request body: " + json, true);

                    using (var response = HttpTransport.Send(request))
                    {
                        string text = HttpTransport.ReadBody(response);
                        trace?.Invoke($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", false);
                        trace?.Invoke("Response body: " + text, true);

                        if (response.StatusCode == HttpStatusCode.Unauthorized && session.CanRefresh && !refreshed)
                        {
                            // The token may have been revoked or expired early; renew it once and try again.
                            session.GetAccessToken(forceRefresh: true);
                            refreshed = true;
                            continue;
                        }

                        if ((int)response.StatusCode == 429 && attempt < MaxThrottleRetries)
                        {
                            var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                            trace?.Invoke($"Throttled by Site24x7, retrying in {wait.TotalSeconds:0} seconds.", false);
                            Thread.Sleep(wait);
                            continue;
                        }

                        return HandleResponse(response, text, raw);
                    }
                }
            }
        }

        internal static Uri BuildUri(Uri baseUri, string path, IDictionary query)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && (absolute.Scheme == "https" || absolute.Scheme == "http"))
            {
                return AppendQuery(absolute, query);
            }

            var relative = (path ?? string.Empty).TrimStart('/');
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
                var value = entry.Value is System.Management.Automation.PSObject pso ? pso.BaseObject : entry.Value;
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
                case System.Management.Automation.PSObject pso:
                    return FormatQueryValue(pso.BaseObject);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static object HandleResponse(HttpResponseMessage response, string text, bool raw)
        {
            object parsed = null;
            bool isJson = true;
            try
            {
                parsed = Json.Parse(text);
            }
            catch (FormatException)
            {
                isJson = false;
            }

            if (!isJson)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Site24x7ApiException(
                        $"Site24x7 returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                        response.StatusCode, null, null, text);
                }
                return text;
            }

            var codeValue = Json.Get(parsed, "code");
            int? code = codeValue == null ? (int?)null : Convert.ToInt32(codeValue, CultureInfo.InvariantCulture);

            if (!response.IsSuccessStatusCode || (code.HasValue && code.Value != 0))
            {
                var message = Json.Get(parsed, "message") as string;
                var errorInfo = Json.Get(parsed, "error_info");
                var detail = string.IsNullOrEmpty(message) ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" : message;
                if (errorInfo != null)
                {
                    detail += " " + Json.Serialize(errorInfo);
                }
                throw new Site24x7ApiException(
                    $"Site24x7 API error{(code.HasValue ? " " + code.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}: {detail}",
                    response.StatusCode, code, errorInfo, text);
            }

            if (raw) return parsed;

            // Successful responses wrap the payload in "data". Some (such as deletes) carry only code and message.
            if (parsed is System.Management.Automation.PSObject obj && obj.Properties["code"] != null)
            {
                return obj.Properties["data"]?.Value;
            }
            return parsed;
        }
    }
}
