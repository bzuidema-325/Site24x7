using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;

namespace Site24x7
{
    /// <summary>The token set returned by Zoho Accounts.</summary>
    public sealed class ZohoToken
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public DateTime ExpiresAt { get; set; }

        public string ApiDomain { get; set; }

        public string TokenType { get; set; }

        public string Scope { get; set; }
    }

    /// <summary>
    /// Zoho Accounts OAuth 2.0 calls used by Site24x7.
    /// https://www.site24x7.com/help/api/#authentication
    /// </summary>
    internal static class ZohoOAuth
    {
        /// <summary>Exchanges a grant code from the Zoho API console (Self Client) for an access and refresh token.</summary>
        public static ZohoToken ExchangeGrantCode(Uri accountsUri, string clientId, string clientSecret, string code)
        {
            return RequestToken(accountsUri, new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
            });
        }

        /// <summary>Gets a new access token. Zoho allows at most 10 new access tokens per 10 minutes.</summary>
        public static ZohoToken RefreshAccessToken(Uri accountsUri, string clientId, string clientSecret, string refreshToken)
        {
            var token = RequestToken(accountsUri, new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" },
            });
            token.RefreshToken = refreshToken;
            return token;
        }

        public static string RevokeRefreshToken(Uri accountsUri, string refreshToken)
        {
            var uri = new Uri(accountsUri, "oauth/v2/token/revoke?token=" + Uri.EscapeDataString(refreshToken));
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            using (var response = HttpTransport.Send(request))
            {
                string body = HttpTransport.ReadBody(response);
                var parsed = TryParse(body);
                ThrowIfError(response, parsed, body);
                return Convert.ToString(Json.Get(parsed, "status"), CultureInfo.InvariantCulture);
            }
        }

        private static ZohoToken RequestToken(Uri accountsUri, Dictionary<string, string> form)
        {
            var uri = new Uri(accountsUri, "oauth/v2/token");
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new FormUrlEncodedContent(form) })
            using (var response = HttpTransport.Send(request))
            {
                string body = HttpTransport.ReadBody(response);
                var parsed = TryParse(body);
                ThrowIfError(response, parsed, body);

                var accessToken = Json.Get(parsed, "access_token") as string;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Site24x7ApiException("Zoho Accounts did not return an access token.", response.StatusCode, null, null, body);
                }

                var expiresIn = Convert.ToDouble(Json.Get(parsed, "expires_in") ?? 3600, CultureInfo.InvariantCulture);
                return new ZohoToken
                {
                    AccessToken = accessToken,
                    RefreshToken = Json.Get(parsed, "refresh_token") as string,
                    ExpiresAt = DateTime.Now.AddSeconds(expiresIn),
                    ApiDomain = Json.Get(parsed, "api_domain") as string,
                    TokenType = Json.Get(parsed, "token_type") as string,
                    Scope = Json.Get(parsed, "scope") as string,
                };
            }
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
        private static void ThrowIfError(HttpResponseMessage response, object parsed, string body)
        {
            var error = Json.Get(parsed, "error") as string;
            if (!string.IsNullOrEmpty(error))
            {
                var hint = error == "invalid_code"
                    ? " The grant code or refresh token is invalid or expired, or it belongs to another data center."
                    : string.Empty;
                throw new Site24x7ApiException($"Zoho Accounts returned '{error}'.{hint}", response.StatusCode, null, null, body);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Site24x7ApiException(
                    $"Zoho Accounts returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    response.StatusCode, null, null, body);
            }
        }
    }
}
