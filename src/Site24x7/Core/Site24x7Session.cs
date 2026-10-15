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
        /// <summary>A token younger than this that the API rejects is not renewed: a new one would be rejected too.</summary>
        internal static readonly TimeSpan MinAgeToRenew = TimeSpan.FromSeconds(60);

        /// <summary>After a failed refresh, how long to wait before asking Zoho again.</summary>
        internal static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(30);

        private readonly object _sync = new object();
        private string _accessToken;
        private DateTime _failedUtc;
        private Exception _failure;

        public TokenSource(Uri accountsUri, string clientId, string clientSecret, string refreshToken, string accessToken)
        {
            AccountsUri = accountsUri;
            ClientId = clientId;
            ClientSecret = clientSecret;
            RefreshToken = refreshToken;
            _accessToken = accessToken;
            ObtainedUtc = DateTime.UtcNow;
        }

        public Uri AccountsUri { get; }

        public string ClientId { get; }

        public string ClientSecret { get; }

        public string RefreshToken { get; }

        public DateTime? ExpiresUtc { get; private set; }

        /// <summary>When the current access token was obtained (or supplied), in UTC.</summary>
        public DateTime ObtainedUtc { get; internal set; }

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
        /// only the first renews it; the others find a new token already in place. A token younger
        /// than <see cref="MinAgeToRenew"/> is not renewed, because Zoho allows only about ten new
        /// tokens per ten minutes and a rejection that soon is not about expiry.
        /// Returns true when the caller should retry with the current token.
        /// </summary>
        public bool RenewRejected(string rejectedToken, CancellationToken cancel)
        {
            if (!CanRefresh) return false;
            lock (_sync)
            {
                if (_accessToken != rejectedToken) return true;
                if (DateTime.UtcNow - ObtainedUtc < MinAgeToRenew) return false;
                Refresh(cancel);
                return true;
            }
        }

        private void Refresh(CancellationToken cancel)
        {
            if (_failure != null && DateTime.UtcNow - _failedUtc < RetryAfterFailure)
            {
                var wait = RetryAfterFailure - (DateTime.UtcNow - _failedUtc);
                throw new Site24x7Exception(
                    $"Renewing the access token failed a moment ago, so the module waits {Math.Ceiling(wait.TotalSeconds):0} more seconds before asking Zoho again: {_failure.Message}",
                    _failure);
            }

            try
            {
                var token = ZohoOAuth.RefreshAccessToken(AccountsUri, ClientId, ClientSecret, RefreshToken, cancel);
                _accessToken = token.AccessToken;
                ExpiresUtc = token.ExpiresUtc;
                ObtainedUtc = DateTime.UtcNow;
                _failure = null;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _failure = ex;
                _failedUtc = DateTime.UtcNow;
                throw;
            }
        }
    }

    /// <summary>
    /// Holds the session that cmdlets use when -Session is not given. Each PowerShell runspace has
    /// its own, so thread jobs, ForEach-Object -Parallel and hosted runspaces do not share a sign-in;
    /// pass -Session to them (for example with $using:session).
    /// </summary>
    internal static class Site24x7SessionState
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
