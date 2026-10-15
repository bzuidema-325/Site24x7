using System;
using System.Net;
using System.Security;
using System.Text.RegularExpressions;

namespace Site24x7
{
    /// <summary>Conversions and checks for the secrets and URLs the module handles.</summary>
    internal static class Secrets
    {
        private static readonly Regex CustomerIdPattern = new Regex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);

        public static string ToPlain(SecureString value) =>
            value == null ? null : new NetworkCredential(string.Empty, value).Password;

        public static SecureString ToSecure(string value)
        {
            if (value == null) return null;
            var secure = new SecureString();
            foreach (char c in value) secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }

        /// <summary>
        /// Customer ids travel in the Cookie header, so anything but letters, digits, '-' and '_'
        /// could add headers or cookies to the request.
        /// </summary>
        public static string CheckCustomerId(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return null;
            if (!CustomerIdPattern.IsMatch(customerId))
            {
                throw new Site24x7ArgumentException($"'{customerId}' is not a valid customer id. Customer ids (zaaid) contain only letters, digits, '-' and '_'.");
            }
            return customerId;
        }

        /// <summary>Credentials and tokens may only go over https, except to this machine (used by tests and local proxies).</summary>
        public static Uri CheckSecureUri(Uri uri, string parameterName)
        {
            if (uri == null) return null;
            if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            {
                throw new Site24x7ArgumentException($"-{parameterName} must be an https URL (http is only allowed for localhost).");
            }
            var s = uri.ToString();
            return s.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(s + "/");
        }
    }
}
