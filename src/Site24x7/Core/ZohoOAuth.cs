using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security;
using System.Threading;

namespace Site24x7
{
    /// <summary>The token set returned by Zoho Accounts. Tokens are SecureStrings so they stay out of logs and transcripts.</summary>
    public sealed class ZohoToken
    {
        public SecureString AccessToken { get; internal set; }

        /// <summary>Only set by Request-Site24x7RefreshToken. Store it with SecretManagement or another secret store.</summary>
        public SecureString RefreshToken { get; internal set; }

        /// <summary>When the access token expires, in UTC.</summary>
        public DateTime ExpiresUtc { get; internal set; }

        public string ApiDomain { get; internal set; }

        public string TokenType { get; internal set; }

        public string Scope { get; internal set; }
    }

    /// <summary>
    /// Zoho Accounts OAuth 2.0 calls used by Site24x7.
    /// https://www.site24x7.com/help/api/#authentication
    /// </summary>
    internal static class ZohoOAuth
    {
        /// <summary>A token response with the tokens still in plain text, for use inside the module only.</summary>
        internal sealed class PlainToken
        {
            public string AccessToken;
            public string RefreshToken;
            public DateTime ExpiresUtc;
            public string ApiDomain;
            public string TokenType;
            public string Scope;

            public ZohoToken ToPublic() => new ZohoToken
            {
                AccessToken = Secrets.ToSecure(AccessToken),
                RefreshToken = Secrets.ToSecure(RefreshToken),
                ExpiresUtc = ExpiresUtc,
                ApiDomain = ApiDomain,
                TokenType = TokenType,
                Scope = Scope,
            };
        }

        /// <summary>Exchanges a grant code from the Zoho API console (Self Client) for an access and refresh token.</summary>
        public static PlainToken ExchangeGrantCode(Uri accountsUri, string clientId, string clientSecret, string code, CancellationToken cancel)
        {
            return RequestToken(accountsUri, new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
            }, cancel);
        }

        /// <summary>Gets a new access token. Zoho allows at most 10 new access tokens per 10 minutes.</summary>
        public static PlainToken RefreshAccessToken(Uri accountsUri, string clientId, string clientSecret, string refreshToken, CancellationToken cancel)
        {
            var token = RequestToken(accountsUri, new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" },
            }, cancel);
            token.RefreshToken = null;
            return token;
        }

        /// <summary>Revokes a refresh token. Zoho documents the token as a query parameter of this call.</summary>
        public static string RevokeRefreshToken(Uri accountsUri, string refreshToken, CancellationToken cancel)
        {
            var uri = new Uri(accountsUri, "oauth/v2/token/revoke?token=" + Uri.EscapeDataString(refreshToken));
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            using (var response = HttpTransport.Send(request, cancel))
            {
                string body = HttpTransport.ReadBody(response);
                var parsed = TryParse(body);
                ThrowIfError(response, parsed, body);
                return Convert.ToString(Json.Get(parsed, "status"), CultureInfo.InvariantCulture);
            }
        }

        private static PlainToken RequestToken(Uri accountsUri, Dictionary<string, string> form, CancellationToken cancel)
        {
            var uri = new Uri(accountsUri, "oauth/v2/token");
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new FormUrlEncodedContent(form) })
            using (var response = HttpTransport.Send(request, cancel))
            {
                string body = HttpTransport.ReadBody(response);
                var parsed = TryParse(body);
                ThrowIfError(response, parsed, body);

                var accessToken = Json.Get(parsed, "access_token") as string;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Site24x7ApiException("Zoho Accounts did not return an access token.", response.StatusCode, null, null, null);
                }

                double expiresIn = ReadLifetimeSeconds(parsed);

                return new PlainToken
                {
                    AccessToken = accessToken,
                    RefreshToken = Json.Get(parsed, "refresh_token") as string,
                    ExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn),
                    ApiDomain = Json.Get(parsed, "api_domain") as string,
                    TokenType = Json.Get(parsed, "token_type") as string,
                    Scope = Json.Get(parsed, "scope") as string,
                };
            }
        }

        /// <summary>
        /// The access token's lifetime in seconds. Zoho has answered with "expires_in" in seconds, and
        /// with "expires_in" in milliseconds next to "expires_in_sec"; anything unusable means one hour.
        /// </summary>
        internal static double ReadLifetimeSeconds(object parsed)
        {
            const double Day = 86400;
            if (TryReadPositive(Json.Get(parsed, "expires_in_sec"), out var seconds) && seconds <= Day)
            {
                return seconds;
            }
            if (TryReadPositive(Json.Get(parsed, "expires_in"), out var value))
            {
                if (value <= Day) return value;
                if (value / 1000 <= Day) return value / 1000;
            }
            return 3600;
        }

        private static bool TryReadPositive(object value, out double number)
        {
            number = 0;
            return value != null &&
                double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
                number > 0 && !double.IsInfinity(number);
        }

        private static object TryParse(string body)
        {
            try
            {
                return Json.Parse(body);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // Zoho reports some failures (for example "invalid_code") with HTTP 200 and an "error" field.
        // The response body is not kept on these errors because a token response can carry secrets.
        private static void ThrowIfError(HttpResponseMessage response, object parsed, string body)
        {
            var error = Json.Get(parsed, "error") as string;
            if (!string.IsNullOrEmpty(error))
            {
                var hint = error == "invalid_code"
                    ? " The grant code or refresh token is invalid or expired, or it belongs to another data center."
                    : string.Empty;
                throw new Site24x7ApiException($"Zoho Accounts returned '{error}'.{hint}", response.StatusCode, null, null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Site24x7ApiException(
                    $"Zoho Accounts returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    response.StatusCode, null, null, parsed == null ? body : null);
            }
        }
    }
}
