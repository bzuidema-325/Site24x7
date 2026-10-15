using System;

namespace Site24x7
{
    /// <summary>
    /// A connection to one Site24x7 account. Created by Connect-Site24x7 and reused by every cmdlet,
    /// so the access token is refreshed automatically when it is about to expire.
    /// </summary>
    public sealed class Site24x7Session
    {
        private readonly object _sync = new object();

        internal Site24x7Session(Site24x7DataCenter dataCenter, Uri apiBaseUri, Uri accountsUri)
        {
            DataCenter = dataCenter;
            ApiBaseUri = EnsureTrailingSlash(apiBaseUri ?? DataCenterEndpoints.ApiBase(dataCenter));
            AccountsUri = EnsureTrailingSlash(accountsUri ?? DataCenterEndpoints.AccountsBase(dataCenter));
        }

        public Site24x7DataCenter DataCenter { get; }

        public Uri ApiBaseUri { get; }

        public Uri AccountsUri { get; }

        public string ClientId { get; internal set; }

        internal string ClientSecret { get; set; }

        internal string RefreshToken { get; set; }

        internal string AccessToken { get; set; }

        /// <summary>When the current access token expires. Null when the token was supplied directly.</summary>
        public DateTime? AccessTokenExpires { get; internal set; }

        /// <summary>True when the session can renew its own access token.</summary>
        public bool CanRefresh => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret) && !string.IsNullOrEmpty(RefreshToken);

        /// <summary>
        /// The MSP customer or Business Unit (zaaid) that requests act on, sent as the zaaid cookie.
        /// Null for the account the token belongs to.
        /// </summary>
        public string CustomerId { get; internal set; }

        /// <summary>Returns a valid access token, refreshing it first if it expires within a minute.</summary>
        internal string GetAccessToken(bool forceRefresh = false)
        {
            lock (_sync)
            {
                bool expiring = AccessTokenExpires.HasValue && AccessTokenExpires.Value <= DateTime.Now.AddSeconds(60);
                if (CanRefresh && (forceRefresh || string.IsNullOrEmpty(AccessToken) || expiring))
                {
                    var token = ZohoOAuth.RefreshAccessToken(AccountsUri, ClientId, ClientSecret, RefreshToken);
                    AccessToken = token.AccessToken;
                    AccessTokenExpires = token.ExpiresAt;
                }

                if (string.IsNullOrEmpty(AccessToken))
                {
                    throw new Site24x7Exception("This session has no access token. Run Connect-Site24x7 again.");
                }

                return AccessToken;
            }
        }

        public override string ToString() => $"{DataCenter} ({ApiBaseUri})";

        private static Uri EnsureTrailingSlash(Uri uri)
        {
            var s = uri.ToString();
            return s.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(s + "/");
        }
    }

    /// <summary>Holds the session that cmdlets use when -Session and -AccessToken are not given.</summary>
    public static class Site24x7SessionState
    {
        public static Site24x7Session Current { get; internal set; }
    }
}
