using System;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Site24x7
{
    /// <summary>
    /// A connection to one Site24x7 account. Created by Connect-Site24x7 and reused by every cmdlet,
    /// so the access token is refreshed automatically when it is about to expire.
    /// Sessions never change after they are created: Set-Site24x7Context makes a new one.
    /// </summary>
    public sealed class Site24x7Session
    {
        internal Site24x7Session(Site24x7DataCenter dataCenter, Uri apiBaseUri, TokenSource tokens, string customerId)
        {
            DataCenter = dataCenter;
            ApiBaseUri = apiBaseUri;
            Tokens = tokens;
            CustomerId = Secrets.CheckCustomerId(customerId);
        }

        public Site24x7DataCenter DataCenter { get; }

        public Uri ApiBaseUri { get; }

        public Uri AccountsUri => Tokens.AccountsUri;

        public string ClientId => Tokens.ClientId;

        /// <summary>When the current access token expires, in UTC. Null when the token was supplied directly.</summary>
        public DateTime? AccessTokenExpires => Tokens.ExpiresUtc;

        /// <summary>True when the session can renew its own access token.</summary>
        public bool CanRefresh => Tokens.CanRefresh;

        /// <summary>
        /// The MSP customer or Business Unit (zaaid) that requests act on, sent as the zaaid cookie.
        /// Null for the account the token belongs to.
        /// </summary>
        public string CustomerId { get; }

        /// <summary>The credentials and token cache, shared by every session derived from the same sign-in.</summary>
        internal TokenSource Tokens { get; }

        /// <summary>The same sign-in, acting on another customer (or on the token's own account when null).</summary>
        internal Site24x7Session WithCustomer(string customerId) => new Site24x7Session(DataCenter, ApiBaseUri, Tokens, customerId);

        public override string ToString() =>
            CustomerId == null ? $"{DataCenter} ({ApiBaseUri})" : $"{DataCenter} ({ApiBaseUri}), customer {CustomerId}";
    }

    /// <summary>
    /// Zoho credentials and the current access token. One instance is shared by a session and
    /// the sessions Set-Site24x7Context derives from it, so they renew the token only once.
    /// </summary>
    internal sealed class TokenSource
    {
        private readonly object _sync = new object();
        private string _accessToken;

        public TokenSource(Uri accountsUri, string clientId, string clientSecret, string refreshToken, string accessToken)
        {
            AccountsUri = accountsUri;
            ClientId = clientId;
            ClientSecret = clientSecret;
            RefreshToken = refreshToken;
            _accessToken = accessToken;
        }

        public Uri AccountsUri { get; }

        public string ClientId { get; }

        public string ClientSecret { get; }

        public string RefreshToken { get; }

        public DateTime? ExpiresUtc { get; private set; }

        public bool CanRefresh => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret) && !string.IsNullOrEmpty(RefreshToken);

        /// <summary>Returns a valid access token, refreshing it first if it expires within a minute.</summary>
        public string GetAccessToken(CancellationToken cancel)
        {
            lock (_sync)
            {
                bool expiring = ExpiresUtc.HasValue && ExpiresUtc.Value <= DateTime.UtcNow.AddSeconds(60);
                if (CanRefresh && (string.IsNullOrEmpty(_accessToken) || expiring))
                {
                    Refresh(cancel);
                }

                if (string.IsNullOrEmpty(_accessToken))
                {
                    throw new Site24x7Exception("This session has no access token. Run Connect-Site24x7 again.");
                }

                return _accessToken;
            }
        }

        /// <summary>
        /// Renews the token after the API rejected it. When several requests are rejected at once,
        /// only the first renews it; the others find a new token already in place.
        /// Returns false when the token cannot be renewed.
        /// </summary>
        public bool RenewRejected(string rejectedToken, CancellationToken cancel)
        {
            if (!CanRefresh) return false;
            lock (_sync)
            {
                if (_accessToken == rejectedToken) Refresh(cancel);
                return true;
            }
        }

        private void Refresh(CancellationToken cancel)
        {
            var token = ZohoOAuth.RefreshAccessToken(AccountsUri, ClientId, ClientSecret, RefreshToken, cancel);
            _accessToken = token.AccessToken;
            ExpiresUtc = token.ExpiresUtc;
        }
    }

    /// <summary>
    /// Holds the session that cmdlets use when -Session is not given. Each PowerShell runspace has
    /// its own, so thread jobs, ForEach-Object -Parallel and hosted runspaces do not share a sign-in;
    /// pass -Session to them (for example with $using:session).
    /// </summary>
    public static class Site24x7SessionState
    {
        private sealed class Holder
        {
            public Site24x7Session Session;
        }

        private static readonly ConditionalWeakTable<Runspace, Holder> ByRunspace = new ConditionalWeakTable<Runspace, Holder>();

        public static Site24x7Session Current
        {
            get
            {
                var runspace = Runspace.DefaultRunspace;
                return runspace != null && ByRunspace.TryGetValue(runspace, out var holder) ? holder.Session : null;
            }
            internal set
            {
                var runspace = Runspace.DefaultRunspace
                    ?? throw new InvalidOperationException("There is no PowerShell runspace on this thread.");
                ByRunspace.GetValue(runspace, _ => new Holder()).Session = value;
            }
        }
    }
}
